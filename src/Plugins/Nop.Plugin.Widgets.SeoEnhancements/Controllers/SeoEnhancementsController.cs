using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;
using Nop.Plugin.Widgets.SeoEnhancements.Models;
using Nop.Plugin.Widgets.SeoEnhancements.Services;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.SeoEnhancements.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class SeoEnhancementsController : BasePluginController
{
    private readonly IFaqService _faqService;
    private readonly IPermissionService _permissionService;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly ILocalizationService _localizationService;

    public SeoEnhancementsController(
        IFaqService faqService,
        IPermissionService permissionService,
        IProductService productService,
        ICategoryService categoryService,
        ILocalizationService localizationService)
    {
        _faqService = faqService;
        _permissionService = permissionService;
        _productService = productService;
        _categoryService = categoryService;
        _localizationService = localizationService;
    }

    // -------------------------------------------------------------------------
    // Configure (landing page)
    // -------------------------------------------------------------------------

    public async Task<IActionResult> Configure()
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
            return AccessDeniedView();

        // Ensure plugin localization resources are added/updated in the database
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Widgets.SeoEnhancements.Configure"] = "SEO Enhancements",
            ["Plugins.Widgets.SeoEnhancements.FAQ.Question"] = "Question",
            ["Plugins.Widgets.SeoEnhancements.FAQ.Answer"] = "Answer",
            ["Plugins.Widgets.SeoEnhancements.FAQ.DisplayOrder"] = "Display Order",
            ["Plugins.Widgets.SeoEnhancements.FAQ.Published"] = "Published",
            ["Plugins.Widgets.SeoEnhancements.FAQ.AddNew"] = "Add FAQ",
            ["Plugins.Widgets.SeoEnhancements.FAQ.Edit"] = "Edit FAQ",
            ["Plugins.Widgets.SeoEnhancements.FAQ.BackToList"] = "Back to FAQ list",
        });

        var searchModel = new FaqItemSearchModel();
        searchModel.SetGridPageSize();

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

        // Fetch all and page in memory (counts will be small per store)
        var allItems = new List<SeoFaqItem>();

        if (searchModel.EntityTypeId.HasValue && searchModel.EntityId.HasValue)
        {
            allItems = (await _faqService.GetFaqItemsAsync(
                (SeoFaqEntityType)searchModel.EntityTypeId.Value,
                searchModel.EntityId.Value,
                publishedOnly: false)).ToList();
        }
        else
        {
            // Load all — gather products and categories FAQ items
            // (For small stores this is fine; extend with a GetAllAsync if needed)
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
