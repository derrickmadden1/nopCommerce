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
    private readonly IShopifyAdminApiService _adminApiService;
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
        IShopifyAdminApiService adminApiService,
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
        _adminApiService = adminApiService;
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
            AdminApiAccessToken = _settings.AdminApiAccessToken,
            ClientId = _settings.ClientId,
            ClientSecret = _settings.ClientSecret,
            ApiVersion = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? ShopifyCheckoutDefaults.DefaultApiVersion : _settings.ApiVersion,
            DisplayButtonOnShoppingCart = _settings.DisplayButtonOnShoppingCart,
            DisplayButtonOnPaymentMethod = _settings.DisplayButtonOnPaymentMethod,
            FallbackToSkuAsVariantId = _settings.FallbackToSkuAsVariantId,
            EnableAutoCatalogSync = _settings.EnableAutoCatalogSync,
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
        _settings.AdminApiAccessToken = model.AdminApiAccessToken;
        _settings.ClientId = model.ClientId;
        _settings.ClientSecret = model.ClientSecret;
        _settings.ApiVersion = model.ApiVersion;
        _settings.DisplayButtonOnShoppingCart = model.DisplayButtonOnShoppingCart;
        _settings.DisplayButtonOnPaymentMethod = model.DisplayButtonOnPaymentMethod;
        _settings.FallbackToSkuAsVariantId = model.FallbackToSkuAsVariantId;
        _settings.EnableAutoCatalogSync = model.EnableAutoCatalogSync;
        _settings.CustomButtonText = model.CustomButtonText;

        await _settingService.SaveSettingAsync(_settings);

        // Auto-generate Storefront Access Token if missing
        if (string.IsNullOrWhiteSpace(_settings.StorefrontAccessToken))
        {
            var (success, token, msg) = await _adminApiService.GetOrCreateStorefrontAccessTokenAsync();
            if (success && !string.IsNullOrWhiteSpace(token))
            {
                _notificationService.SuccessNotification($"Storefront Access Token automatically generated and saved!");
            }
        }

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return Configure();
    }

    [HttpPost]
    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [CheckPermission(StandardPermission.Configuration.MANAGE_WIDGETS)]
    public async Task<IActionResult> AutoGenerateStorefrontToken()
    {
        var (success, token, msg) = await _adminApiService.GetOrCreateStorefrontAccessTokenAsync();
        if (success)
        {
            _notificationService.SuccessNotification(msg);
        }
        else
        {
            _notificationService.ErrorNotification(msg);
        }

        return RedirectToAction("Configure");
    }

    [HttpPost]
    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [CheckPermission(StandardPermission.Configuration.MANAGE_WIDGETS)]
    public async Task<IActionResult> RunFullCatalogSync()
    {
        var (totalProcessed, syncedCount, failedCount, logs) = await _adminApiService.FullCatalogSyncAsync();

        if (failedCount == 0)
        {
            _notificationService.SuccessNotification($"Full catalog sync completed successfully! Processed {totalProcessed} items ({syncedCount} synced).");
        }
        else
        {
            _notificationService.WarningNotification($"Full catalog sync finished with warnings: {syncedCount} synced, {failedCount} failed out of {totalProcessed}. Check System Log for details.");
        }

        return RedirectToAction("Configure");
    }

    #endregion

    #region Public Methods

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> InitCheckout()
    {
        if (string.IsNullOrWhiteSpace(_settings.StoreUrl) || string.IsNullOrWhiteSpace(_settings.StorefrontAccessToken))
        {
            if (!string.IsNullOrWhiteSpace(_settings.StoreUrl))
            {
                var (success, token, _) = await _adminApiService.GetOrCreateStorefrontAccessTokenAsync();
                if (success && !string.IsNullOrWhiteSpace(token))
                {
                    _settings.StorefrontAccessToken = token;
                }
            }

            if (string.IsNullOrWhiteSpace(_settings.StoreUrl) || string.IsNullOrWhiteSpace(_settings.StorefrontAccessToken))
            {
                _notificationService.ErrorNotification("Shopify Checkout is not configured yet. Please configure the Store URL and Storefront Access Token in Admin panel.");
                return RedirectToRoute("ShoppingCart");
            }
        }

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
