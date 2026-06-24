using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;
using Nop.Plugin.Widgets.SeoEnhancements.Models;
using Nop.Plugin.Widgets.SeoEnhancements.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.SeoEnhancements.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class SeoEnhancementsController : BasePluginController
{
    private readonly IBaseAdminModelFactory _baseAdminModelFactory;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IWorkContext _workContext;
    private readonly IFaqService _faqService;
    private readonly IFaqGenerationService _faqGenerationService;
    private readonly IPermissionService _permissionService;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly ISettingService _settingService;
    private readonly SeoEnhancementsSettings _settings;
    private readonly INotificationService _notificationService;

    public SeoEnhancementsController(
        IBaseAdminModelFactory baseAdminModelFactory,
        IGenericAttributeService genericAttributeService,
        IWorkContext workContext,
        IFaqService faqService,
        IFaqGenerationService faqGenerationService,
        IPermissionService permissionService,
        IProductService productService,
        ICategoryService categoryService,
        ISettingService settingService,
        SeoEnhancementsSettings settings,
        INotificationService notificationService)
    {
        _baseAdminModelFactory = baseAdminModelFactory;
        _genericAttributeService = genericAttributeService;
        _workContext = workContext;
        _faqService = faqService;
        _faqGenerationService = faqGenerationService;
        _permissionService = permissionService;
        _productService = productService;
        _categoryService = categoryService;
        _settingService = settingService;
        _settings = settings;
        _notificationService = notificationService;
    }

    // -------------------------------------------------------------------------
    // Configure (landing page)
    // -------------------------------------------------------------------------

    public async Task<IActionResult> Configure()
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        var searchModel = new FaqItemSearchModel();
        searchModel.SetGridPageSize();

        //load saved search criteria
        var currentCustomer = await _workContext.GetCurrentCustomerAsync();
        var savedCriteriaStr = await _genericAttributeService.GetAttributeAsync<string>(currentCustomer, "Admin.FaqSearchCriteria");
        if (!string.IsNullOrEmpty(savedCriteriaStr))
        {
            var savedCriteria = Newtonsoft.Json.JsonConvert.DeserializeObject<FaqItemSearchModel>(savedCriteriaStr);
            if (savedCriteria != null)
            {
                searchModel.SearchKeywords = savedCriteria.SearchKeywords;
                searchModel.SearchCategoryId = savedCriteria.SearchCategoryId;
                searchModel.SearchPublishedId = savedCriteria.SearchPublishedId;
            }
        }

        //prepare available categories
        await _baseAdminModelFactory.PrepareCategoriesAsync(searchModel.AvailableCategories);

        //prepare "published" filter options
        var localizationService = Nop.Core.Infrastructure.EngineContext.Current.Resolve<Nop.Services.Localization.ILocalizationService>();
        searchModel.AvailablePublishedOptions.Add(new SelectListItem
        {
            Value = "0",
            Text = await localizationService.GetResourceAsync("Admin.Catalog.Products.List.SearchPublished.All")
        });
        searchModel.AvailablePublishedOptions.Add(new SelectListItem
        {
            Value = "1",
            Text = await localizationService.GetResourceAsync("Admin.Catalog.Products.List.SearchPublished.PublishedOnly")
        });
        searchModel.AvailablePublishedOptions.Add(new SelectListItem
        {
            Value = "2",
            Text = await localizationService.GetResourceAsync("Admin.Catalog.Products.List.SearchPublished.UnpublishedOnly")
        });

        return View("~/Plugins/Widgets.SeoEnhancements/Views/SeoEnhancements/Configure.cshtml", searchModel);
    }

    // -------------------------------------------------------------------------
    // FAQ list (ajax grid)
    // -------------------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> FaqList(FaqItemSearchModel searchModel)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return await AccessDeniedJsonAsync();

        //save search criteria
        var customer = await _workContext.GetCurrentCustomerAsync();
        await _genericAttributeService.SaveAttributeAsync(customer, "Admin.FaqSearchCriteria", Newtonsoft.Json.JsonConvert.SerializeObject(searchModel));

        // Fetch all
        var allItems = (await _faqService.GetAllFaqItemsAsync(publishedOnly: false)).ToList();

        // Apply filters
        // 1. Published filter
        if (searchModel.SearchPublishedId == 1)
        {
            allItems = allItems.Where(x => x.Published).ToList();
        }
        else if (searchModel.SearchPublishedId == 2)
        {
            allItems = allItems.Where(x => !x.Published).ToList();
        }

        // 2. Category filter
        if (searchModel.SearchCategoryId > 0)
        {
            // prefetch all product mappings for the category
            var productCategories = await _categoryService.GetProductCategoriesByCategoryIdAsync(searchModel.SearchCategoryId, showHidden: true);
            var productIdsInCategory = new System.Collections.Generic.HashSet<int>(productCategories.Select(pc => pc.ProductId));

            allItems = allItems.Where(item =>
                (item.EntityTypeId == (int)SeoFaqEntityType.Category && item.EntityId == searchModel.SearchCategoryId) ||
                (item.EntityTypeId == (int)SeoFaqEntityType.Product && productIdsInCategory.Contains(item.EntityId))
            ).ToList();
        }

        // 3. Keywords filter
        if (!string.IsNullOrWhiteSpace(searchModel.SearchKeywords))
        {
            var filteredItems = new List<SeoFaqItem>();
            foreach (var item in allItems)
            {
                if (item.Question.Contains(searchModel.SearchKeywords, StringComparison.InvariantCultureIgnoreCase) ||
                    item.Answer.Contains(searchModel.SearchKeywords, StringComparison.InvariantCultureIgnoreCase))
                {
                    filteredItems.Add(item);
                    continue;
                }

                // Check entity name
                string entityName = "";
                if (item.EntityTypeId == (int)SeoFaqEntityType.Product)
                {
                    var product = await _productService.GetProductByIdAsync(item.EntityId);
                    entityName = product?.Name ?? "";
                }
                else if (item.EntityTypeId == (int)SeoFaqEntityType.Category)
                {
                    var category = await _categoryService.GetCategoryByIdAsync(item.EntityId);
                    entityName = category?.Name ?? "";
                }

                if (!string.IsNullOrEmpty(entityName) && entityName.Contains(searchModel.SearchKeywords, StringComparison.InvariantCultureIgnoreCase))
                {
                    filteredItems.Add(item);
                }
            }
            allItems = filteredItems;
        }

        var pagedItems = allItems
            .Skip((searchModel.Page - 1) * searchModel.PageSize)
            .Take(searchModel.PageSize)
            .ToList();

        var models = new List<FaqItemModel>();
        foreach (var item in pagedItems)
        {
            var model = await MapToModelAsync(item);
            models.Add(model);
        }

        return Json(new FaqItemListModel
        {
            Data = models,
            Draw = searchModel.Draw,
            RecordsTotal = allItems.Count,
            RecordsFiltered = allItems.Count
        });
    }

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    public async Task<IActionResult> Create()
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        var model = new FaqItemModel { Published = true };
        await PrepareEntityDropdownsAsync(model);
        return View("~/Plugins/Widgets.SeoEnhancements/Views/SeoEnhancements/Create.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(FaqItemModel model)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        if (ModelState.IsValid)
        {
            var item = new SeoFaqItem
            {
                EntityTypeId = model.EntityTypeId,
                EntityId = model.EntityId,
                Question = model.Question,
                Answer = model.Answer,
                DisplayOrder = model.DisplayOrder,
                Published = model.Published
            };
            await _faqService.InsertFaqItemAsync(item);
            return RedirectToAction(nameof(Configure));
        }

        await PrepareEntityDropdownsAsync(model);
        return View("~/Plugins/Widgets.SeoEnhancements/Views/SeoEnhancements/Create.cshtml", model);
    }

    // -------------------------------------------------------------------------
    // Edit
    // -------------------------------------------------------------------------

    public async Task<IActionResult> Edit(int id)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        var item = await _faqService.GetFaqItemByIdAsync(id);
        if (item == null)
            return RedirectToAction(nameof(Configure));

        var model = await MapToModelAsync(item);
        await PrepareEntityDropdownsAsync(model);
        return View("~/Plugins/Widgets.SeoEnhancements/Views/SeoEnhancements/Edit.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(FaqItemModel model)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        var item = await _faqService.GetFaqItemByIdAsync(model.Id);
        if (item == null)
            return RedirectToAction(nameof(Configure));

        if (ModelState.IsValid)
        {
            item.EntityTypeId = model.EntityTypeId;
            item.EntityId = model.EntityId;
            item.Question = model.Question;
            item.Answer = model.Answer;
            item.DisplayOrder = model.DisplayOrder;
            item.Published = model.Published;
            await _faqService.UpdateFaqItemAsync(item);
            return RedirectToAction(nameof(Configure));
        }

        await PrepareEntityDropdownsAsync(model);
        return View("~/Plugins/Widgets.SeoEnhancements/Views/SeoEnhancements/Edit.cshtml", model);
    }

    // -------------------------------------------------------------------------
    // Delete
    // -------------------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        var item = await _faqService.GetFaqItemByIdAsync(id);
        if (item != null)
            await _faqService.DeleteFaqItemAsync(item);

        return RedirectToAction(nameof(Configure));
    }

    // -------------------------------------------------------------------------
    // AI generation — review screen
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called via the "Generate FAQs with AI" button on a product/category.
    /// Calls Azure OpenAI, then shows a review screen where each pair can be
    /// included/excluded and edited before saving.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Generate(FaqGenerateRequestModel request)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        var reviewModel = new FaqGenerateReviewModel
        {
            EntityTypeId = request.EntityTypeId,
            EntityId = request.EntityId
        };

        IList<GeneratedFaqPair> generated;

        if (request.EntityTypeId == (int)SeoFaqEntityType.Product)
        {
            var product = await _productService.GetProductByIdAsync(request.EntityId);
            if (product == null)
            {
                _notificationService.ErrorNotification("Product not found.");
                return RedirectToAction(nameof(Configure));
            }
            reviewModel.EntityName = product.Name;
            generated = await _faqGenerationService.GenerateForProductAsync(product, _settings.FaqPairsToGenerate);
        }
        else
        {
            var category = await _categoryService.GetCategoryByIdAsync(request.EntityId);
            if (category == null)
            {
                _notificationService.ErrorNotification("Category not found.");
                return RedirectToAction(nameof(Configure));
            }
            reviewModel.EntityName = category.Name;
            generated = await _faqGenerationService.GenerateForCategoryAsync(category, _settings.FaqPairsToGenerate);
        }

        if (!generated.Any())
        {
            _notificationService.ErrorNotification("No FAQs were generated. Check your Azure OpenAI settings and the admin log for details.");
            return RedirectToAction(nameof(Configure));
        }

        reviewModel.Candidates = generated
            .Select(g => new GeneratedFaqReviewItem { Question = g.Question, Answer = g.Answer, Include = true })
            .ToList();

        return View("~/Plugins/Widgets.SeoEnhancements/Views/SeoEnhancements/Review.cshtml", reviewModel);
    }

    /// <summary>
    /// Saves the FAQ pairs the admin kept checked on the review screen.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveGenerated(FaqGenerateReviewModel model)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        var displayOrder = 0;
        foreach (var candidate in model.Candidates.Where(c => c.Include))
        {
            await _faqService.InsertFaqItemAsync(new SeoFaqItem
            {
                EntityTypeId = model.EntityTypeId,
                EntityId = model.EntityId,
                Question = candidate.Question,
                Answer = candidate.Answer,
                DisplayOrder = displayOrder++,
                Published = true
            });
        }

        _notificationService.SuccessNotification($"{displayOrder} FAQ item(s) saved.");
        return RedirectToAction(nameof(Configure));
    }

    // -------------------------------------------------------------------------
    // Settings (Azure OpenAI configuration)
    // -------------------------------------------------------------------------

    public async Task<IActionResult> Settings()
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        var model = new SeoEnhancementsConfigModel
        {
            AzureOpenAiEndpoint = _settings.AzureOpenAiEndpoint,
            AzureOpenAiApiKey = _settings.AzureOpenAiApiKey,
            AzureOpenAiDeploymentName = _settings.AzureOpenAiDeploymentName,
            AzureOpenAiApiVersion = _settings.AzureOpenAiApiVersion,
            FaqPairsToGenerate = _settings.FaqPairsToGenerate,
            UseAzureKeyVault = _settings.UseAzureKeyVault,
            AzureKeyVaultUrl = _settings.AzureKeyVaultUrl,
            AzureKeyVaultSecretName = _settings.AzureKeyVaultSecretName
        };

        return View("~/Plugins/Widgets.SeoEnhancements/Views/SeoEnhancements/Settings.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Settings(SeoEnhancementsConfigModel model)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        _settings.AzureOpenAiEndpoint = model.AzureOpenAiEndpoint;
        _settings.AzureOpenAiApiKey = model.AzureOpenAiApiKey;
        _settings.AzureOpenAiDeploymentName = model.AzureOpenAiDeploymentName;
        _settings.AzureOpenAiApiVersion = model.AzureOpenAiApiVersion;
        _settings.FaqPairsToGenerate = model.FaqPairsToGenerate;
        _settings.UseAzureKeyVault = model.UseAzureKeyVault;
        _settings.AzureKeyVaultUrl = model.AzureKeyVaultUrl;
        _settings.AzureKeyVaultSecretName = model.AzureKeyVaultSecretName;

        await _settingService.SaveSettingAsync(_settings);

        _notificationService.SuccessNotification("Settings saved.");
        return RedirectToAction(nameof(Settings));
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<FaqItemModel> MapToModelAsync(SeoFaqItem item)
    {
        var model = new FaqItemModel
        {
            Id = item.Id,
            EntityTypeId = item.EntityTypeId,
            EntityId = item.EntityId,
            Question = item.Question,
            Answer = item.Answer,
            DisplayOrder = item.DisplayOrder,
            Published = item.Published
        };

        if (item.EntityTypeId == (int)SeoFaqEntityType.Product)
        {
            var product = await _productService.GetProductByIdAsync(item.EntityId);
            model.EntityName = product?.Name ?? $"Product #{item.EntityId}";
        }
        else
        {
            var category = await _categoryService.GetCategoryByIdAsync(item.EntityId);
            model.EntityName = category?.Name ?? $"Category #{item.EntityId}";
        }

        return model;
    }

    private async Task PrepareEntityDropdownsAsync(FaqItemModel model)
    {
        ViewBag.EntityTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "1", Text = "Product", Selected = model.EntityTypeId == 1 },
            new SelectListItem { Value = "2", Text = "Category", Selected = model.EntityTypeId == 2 }
        };
    }
}
