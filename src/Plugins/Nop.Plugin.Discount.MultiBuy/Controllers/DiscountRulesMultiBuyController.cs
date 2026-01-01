using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Discounts;
using Nop.Plugin.DiscountRules.MultiBuy.Models;
using Nop.Plugin.DiscountRules.MultiBuy.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.DiscountRules.MultiBuy.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class DiscountRulesMultiBuyController : BasePluginController
{
    #region Fields

    private const char IDS_SEPARATOR = ',';
    private const char QUANTITY_SEPARATOR = ':';
    private readonly IDiscountService _discountService;
    private readonly IProductModelFactory _productModelFactory;
    private readonly IProductService _productService;
    private readonly ISettingService _settingService;

    #endregion

    #region Ctor

    public DiscountRulesMultiBuyController(IDiscountService discountService,
        IProductModelFactory productModelFactory,
        IProductService productService,
        ISettingService settingService)
    {
        _discountService = discountService;
        _productModelFactory = productModelFactory;
        _productService = productService;
        _settingService = settingService;
    }

    #endregion

    #region Utilities

    private IEnumerable<string> GetErrorsFromModelState()
    {
        return ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
    }

    #endregion

    #region Methods

    [CheckPermission(StandardPermission.Promotions.DISCOUNTS_VIEW)]
    public async Task<IActionResult> Configure(int discountId, int? discountRequirementId)
    {
        // when called from the plugin list, there is no specific discount context,
        // so just redirect admins to the discounts page instead of throwing
        if (discountId <= 0)
            return RedirectToAction("List", "Discount");

        //load the discount
        var discount = await _discountService.GetDiscountByIdAsync(discountId) ?? throw new ArgumentException("Discount could not be loaded");

        //check whether the discount requirement exists
        if (discountRequirementId.HasValue && await _discountService.GetDiscountRequirementByIdAsync(discountRequirementId.Value) is null)
            return Content("Failed to load requirement.");

        //load per-requirement settings (if any)
        var settingsKey = string.Format(DiscountRequirementDefaults.SETTINGS_KEY, discountRequirementId ?? 0);
        var requirementSettingsJson = await _settingService.GetSettingByKeyAsync<string>(settingsKey);
        MultiBuyRequirementSettings requirementSettings = null;
        if (!string.IsNullOrEmpty(requirementSettingsJson))
        {
            try
            {
                requirementSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<MultiBuyRequirementSettings>(requirementSettingsJson);
            }
            catch
            {
                //settings were likely saved in an old format (e.g. "Nop.Plugin...") or are corrupted
                //ignore exception, we'll use default settings
            }
        }
        
        requirementSettings ??= new MultiBuyRequirementSettings();

        var model = new ConfigureModel
        {
            RequirementId = discountRequirementId ?? 0,
            DiscountId = discount.Id,
            ProductIds = string.Join(",", requirementSettings.EligibleProductIds),
            BundleSize = requirementSettings.BundleSize,
            BundlePrice = requirementSettings.BundlePrice,
            ApplyAcrossMixedProducts = requirementSettings.ApplyAcrossMixedProducts
        };

        //set the HTML field prefix
        ViewData.TemplateInfo.HtmlFieldPrefix = string.Format(DiscountRequirementDefaults.HTML_FIELD_PREFIX, discountRequirementId ?? 0);

        return View("~/Plugins/DiscountRules.MultiBuy/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Promotions.DISCOUNTS_CREATE_EDIT_DELETE)]
    public async Task<IActionResult> Configure(ConfigureModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { Errors = GetErrorsFromModelState() });

        if (model.BundleSize < 1)
            ModelState.AddModelError(nameof(model.BundleSize), "Bundle size must be at least 1.");

        if (model.BundlePrice <= 0)
            ModelState.AddModelError(nameof(model.BundlePrice), "Bundle price must be greater than zero.");

        if (!ModelState.IsValid)
            return BadRequest(new { Errors = GetErrorsFromModelState() });

        //load the discount
        var discount = await _discountService.GetDiscountByIdAsync(model.DiscountId);
        if (discount == null)
            return NotFound(new { Errors = new[] { "Discount could not be loaded" } });

        //get the discount requirement
        var discountRequirement = await _discountService.GetDiscountRequirementByIdAsync(model.RequirementId);

        //the discount requirement does not exist, so create a new one
        if (discountRequirement == null)
        {
            discountRequirement = new DiscountRequirement
            {
                DiscountId = discount.Id,
                DiscountRequirementRuleSystemName = DiscountRequirementDefaults.SYSTEM_NAME
            };

            await _discountService.InsertDiscountRequirementAsync(discountRequirement);
        }

        //parse eligible product IDs from the string
        var eligibleProductIds = new List<int>();
        if (!string.IsNullOrWhiteSpace(model.ProductIds))
        {
            var split = model.ProductIds
                .Split(IDS_SEPARATOR, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());

            foreach (var part in split)
            {
                if (int.TryParse(part, out var id))
                    eligibleProductIds.Add(id);
            }
        }

        var settings = new MultiBuyRequirementSettings
        {
            BundleSize = model.BundleSize,
            BundlePrice = model.BundlePrice,
            ApplyAcrossMixedProducts = model.ApplyAcrossMixedProducts,
            EligibleProductIds = eligibleProductIds
        };

        var settingsKey = string.Format(DiscountRequirementDefaults.SETTINGS_KEY, discountRequirement.Id);
        await _settingService.SetSettingAsync(settingsKey, Newtonsoft.Json.JsonConvert.SerializeObject(settings));

        return Ok(new { NewRequirementId = discountRequirement.Id });
    }

    [CheckPermission(StandardPermission.Catalog.PRODUCTS_VIEW)]
    public async Task<IActionResult> ProductAddPopup()
    {
        //prepare model
        var model = await _productModelFactory.PrepareProductSearchModelAsync(new ProductSearchModel());

        return View("~/Plugins/DiscountRules.MultiBuy/Views/ProductAddPopup.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Catalog.PRODUCTS_VIEW)]
    public async Task<IActionResult> LoadProductFriendlyNames(string productIds)
    {
        if (string.IsNullOrWhiteSpace(productIds))
            return Json(new { Text = string.Empty });

        var parsedIds = new List<int>();
        var rangeArray = productIds.Split(IDS_SEPARATOR, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();

        //we support three ways of specifying products:
        //1. The comma-separated list of product identifiers (e.g. 77, 123, 156).
        //2. The comma-separated list of product identifiers with quantities.
        //      {Product ID}:{Quantity}. For example, 77:1, 123:2, 156:3
        //3. The comma-separated list of product identifiers with quantity range.
        //      {Product ID}:{Min quantity}-{Max quantity}. For example, 77:1-3, 123:2-5, 156:3-8
        foreach (var productQuantityPair in rangeArray)
        {
            var temp = productQuantityPair;

            //we do not display specified quantities and ranges
            //so let's parse only product names (before : sign)
            if (productQuantityPair.Contains(':'))
                temp = productQuantityPair.Split(QUANTITY_SEPARATOR, StringSplitOptions.RemoveEmptyEntries)[0];

            if (int.TryParse(temp, out var productId))
                parsedIds.Add(productId);
        }

        var products = await _productService.GetProductsByIdsAsync(parsedIds.ToArray());
        var productNames = string.Join(", ", products.Select(p => p.Name));

        return Json(new { Text = productNames });
    }

    #endregion
}