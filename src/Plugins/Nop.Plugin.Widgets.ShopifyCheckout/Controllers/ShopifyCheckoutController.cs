using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Widgets.ShopifyCheckout.Models;
using Nop.Plugin.Widgets.ShopifyCheckout.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Controllers;

public class ShopifyCheckoutController : BasePluginController
{
    #region Fields

    private readonly IWorkContext _workContext;
    private readonly IStoreContext _storeContext;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IProductService _productService;
    private readonly IShopifyVariantMappingService _variantMappingService;
    private readonly IShopifyStorefrontService _shopifyStorefrontService;
    private readonly IShopifyOrderSyncService _orderSyncService;
    private readonly ShopifyCheckoutSettings _settings;
    private readonly ISettingService _settingService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IPermissionService _permissionService;

    #endregion

    #region Ctor

    public ShopifyCheckoutController(
        IWorkContext workContext,
        IStoreContext storeContext,
        IShoppingCartService shoppingCartService,
        IProductService productService,
        IShopifyVariantMappingService variantMappingService,
        IShopifyStorefrontService shopifyStorefrontService,
        IShopifyOrderSyncService orderSyncService,
        ShopifyCheckoutSettings settings,
        ISettingService settingService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        IPermissionService permissionService)
    {
        _workContext = workContext;
        _storeContext = storeContext;
        _shoppingCartService = shoppingCartService;
        _productService = productService;
        _variantMappingService = variantMappingService;
        _shopifyStorefrontService = shopifyStorefrontService;
        _orderSyncService = orderSyncService;
        _settings = settings;
        _settingService = settingService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _permissionService = permissionService;
    }

    #endregion

    #region Admin Methods

    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [CheckPermission(StandardPermission.Configuration.MANAGE_WIDGETS)]
    public IActionResult Configure()
    {
        var model = new ConfigurationModel
        {
            StoreUrl = _settings.StoreUrl,
            StorefrontAccessToken = _settings.StorefrontAccessToken,
            ApiVersion = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? ShopifyCheckoutDefaults.DefaultApiVersion : _settings.ApiVersion,
            DisplayButtonOnShoppingCart = _settings.DisplayButtonOnShoppingCart,
            DisplayButtonOnPaymentMethod = _settings.DisplayButtonOnPaymentMethod,
            FallbackToSkuAsVariantId = _settings.FallbackToSkuAsVariantId,
            CustomButtonText = string.IsNullOrWhiteSpace(_settings.CustomButtonText) ? "Checkout with Shopify" : _settings.CustomButtonText
        };

        return View("~/Plugins/Widgets.ShopifyCheckout/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [CheckPermission(StandardPermission.Configuration.MANAGE_WIDGETS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return Configure();

        _settings.StoreUrl = model.StoreUrl;
        _settings.StorefrontAccessToken = model.StorefrontAccessToken;
        _settings.ApiVersion = model.ApiVersion;
        _settings.DisplayButtonOnShoppingCart = model.DisplayButtonOnShoppingCart;
        _settings.DisplayButtonOnPaymentMethod = model.DisplayButtonOnPaymentMethod;
        _settings.FallbackToSkuAsVariantId = model.FallbackToSkuAsVariantId;
        _settings.CustomButtonText = model.CustomButtonText;

        await _settingService.SaveSettingAsync(_settings);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return Configure();
    }

    #endregion

    #region Public Methods

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> InitCheckout()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
        if (!cart.Any())
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("ShoppingCart.CartIsEmpty"));
            return RedirectToRoute("ShoppingCart");
        }

        var lineItems = new List<(string MerchandiseId, int Quantity)>();
        var unmappedItems = new List<string>();

        foreach (var item in cart)
        {
            var variantGid = await _variantMappingService.GetShopifyVariantGidAsync(item);
            if (string.IsNullOrWhiteSpace(variantGid))
            {
                var product = await _productService.GetProductByIdAsync(item.ProductId);
                unmappedItems.Add(product?.Name ?? $"Product #{item.ProductId}");
            }
            else
            {
                lineItems.Add((variantGid, item.Quantity));
            }
        }

        if (unmappedItems.Any())
        {
            var unmappedMsg = string.Join(", ", unmappedItems);
            _notificationService.ErrorNotification($"The following items cannot be checked out via Shopify (missing Shopify Variant ID mapping): {unmappedMsg}");
            return RedirectToRoute("ShoppingCart");
        }

        var (checkoutUrl, errors) = await _shopifyStorefrontService.CreateCartAsync(lineItems);

        if (errors != null && errors.Any())
        {
            foreach (var err in errors)
            {
                _notificationService.ErrorNotification($"Shopify Checkout Error: {err}");
            }
            return RedirectToRoute("ShoppingCart");
        }

        // Clear local nopCommerce shopping cart session upon successful handoff
        await _shoppingCartService.ClearShoppingCartAsync(customer, store.Id);

        return Redirect(checkoutUrl);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ProcessOrderWebhook([FromBody] ShopifyWebhookOrderModel model)
    {
        var (success, message, count) = await _orderSyncService.ProcessShopifyOrderAsync(model);
        if (!success)
            return BadRequest(new { success = false, message });

        return Ok(new { success = true, message, syncedItemsCount = count });
    }

    #endregion
}
