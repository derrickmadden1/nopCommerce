using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Widgets.ShopifyCheckout.Models;
using Nop.Plugin.Widgets.ShopifyCheckout.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
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
    private readonly IPriceCalculationService _priceCalculationService;
    private readonly IOrderTotalCalculationService _orderTotalCalculationService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IShopifyVariantMappingService _variantMappingService;
    private readonly IShopifyStorefrontService _shopifyStorefrontService;
    private readonly IShopifyOrderSyncService _orderSyncService;
    private readonly IShopifyAdminApiService _adminApiService;
    private readonly ShopifyCheckoutSettings _settings;
    private readonly ISettingService _settingService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger _logger;

    #endregion

    #region Ctor

    public ShopifyCheckoutController(
        IWorkContext workContext,
        IStoreContext storeContext,
        IShoppingCartService shoppingCartService,
        IProductService productService,
        IPriceCalculationService priceCalculationService,
        IOrderTotalCalculationService orderTotalCalculationService,
        IGenericAttributeService genericAttributeService,
        IShopifyVariantMappingService variantMappingService,
        IShopifyStorefrontService shopifyStorefrontService,
        IShopifyOrderSyncService orderSyncService,
        IShopifyAdminApiService adminApiService,
        ShopifyCheckoutSettings settings,
        ISettingService settingService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        IPermissionService permissionService,
        ILogger logger)
    {
        _workContext = workContext;
        _storeContext = storeContext;
        _shoppingCartService = shoppingCartService;
        _productService = productService;
        _priceCalculationService = priceCalculationService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _genericAttributeService = genericAttributeService;
        _variantMappingService = variantMappingService;
        _shopifyStorefrontService = shopifyStorefrontService;
        _orderSyncService = orderSyncService;
        _adminApiService = adminApiService;
        _settings = settings;
        _settingService = settingService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _permissionService = permissionService;
        _logger = logger;
    }

    #endregion

    #region Admin Methods

    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [CheckPermission(StandardPermission.Configuration.MANAGE_WIDGETS)]
    public async Task<IActionResult> Configure()
    {
        await EnsureLocaleResourcesAsync();

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

    private async Task EnsureLocaleResourcesAsync()
    {
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.StoreUrl", "Shopify Store URL");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.StorefrontAccessToken", "Storefront Access Token");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.StorefrontAccessToken.Hint", "Enter your Storefront API Access Token (e.g. from Shopify Headless Sales Channel or App API credentials).");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.AdminApiAccessToken", "Admin API Access Token");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.ClientId", "Shopify App Client ID");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.ClientId.Hint", "Enter Client ID (API Key) for OAuth Client Credentials grant.");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.ClientSecret", "Shopify App Client Secret");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.ClientSecret.Hint", "Enter Client Secret (API Secret Key) for OAuth Client Credentials grant.");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.ApiVersion", "API Version");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.DisplayButtonOnShoppingCart", "Display on Shopping Cart");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.DisplayButtonOnPaymentMethod", "Display on Checkout Payment Page");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.FallbackToSkuAsVariantId", "Fallback to SKU as Variant ID");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.EnableAutoCatalogSync", "Auto-Sync Products to Shopify");
        await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Widgets.ShopifyCheckout.Fields.CustomButtonText", "Checkout Button Text");
    }

    [HttpPost]
    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [CheckPermission(StandardPermission.Configuration.MANAGE_WIDGETS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

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
        _adminApiService.ClearTokenCache();

        // Auto-generate Storefront Access Token if missing
        if (string.IsNullOrWhiteSpace(_settings.StorefrontAccessToken))
        {
            var (success, token, msg) = await _adminApiService.GetOrCreateStorefrontAccessTokenAsync();
            if (success && !string.IsNullOrWhiteSpace(token))
            {
                _notificationService.SuccessNotification($"Storefront Access Token automatically generated and saved!");
            }
            else
            {
                _notificationService.WarningNotification($"Could not auto-generate Storefront Access Token: {msg}");
            }
        }

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
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
        await _logger.InformationAsync("Initiating Shopify Checkout handoff...");

        if (string.IsNullOrWhiteSpace(_settings.StoreUrl) || string.IsNullOrWhiteSpace(_settings.StorefrontAccessToken))
        {
            if (!string.IsNullOrWhiteSpace(_settings.StoreUrl))
            {
                var (tokenOk, token, _) = await _adminApiService.GetOrCreateStorefrontAccessTokenAsync();
                if (tokenOk && !string.IsNullOrWhiteSpace(token))
                {
                    _settings.StorefrontAccessToken = token;
                }
            }

            if (string.IsNullOrWhiteSpace(_settings.StoreUrl))
            {
                var errMsg = "Shopify Checkout is not configured yet. Please configure the Store URL in Admin panel.";
                await _logger.WarningAsync(errMsg);
                _notificationService.ErrorNotification(errMsg);
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

        var draftItems = new List<(string VariantGid, int Quantity, decimal UnitPrice, decimal OriginalListPrice)>();
        var unmappedItems = new List<string>();

        foreach (var item in cart)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            var variantGid = await _variantMappingService.GetShopifyVariantGidAsync(item);
            if (string.IsNullOrWhiteSpace(variantGid))
            {
                unmappedItems.Add(product?.Name ?? $"Product #{item.ProductId}");
            }
            else
            {
                // Calculate original unit price without discounts
                var (originalSubTotal, _, _, _) = await _shoppingCartService.GetSubTotalAsync(item, includeDiscounts: false);
                var originalListPrice = item.Quantity > 0 ? originalSubTotal / item.Quantity : product.Price;

                // Calculate final unit price after item-level discounts (puzzle, multi-buy, customer role, category, etc.)
                var (itemSubTotal, _, _, _) = await _shoppingCartService.GetSubTotalAsync(item, includeDiscounts: true);
                var unitPrice = item.Quantity > 0 ? itemSubTotal / item.Quantity : itemSubTotal;

                draftItems.Add((variantGid, item.Quantity, unitPrice, originalListPrice));
            }
        }

        if (unmappedItems.Any())
        {
            var unmappedMsg = string.Join(", ", unmappedItems);
            var errMsg = $"The following items cannot be checked out via Shopify (missing Shopify Variant ID mapping): {unmappedMsg}";
            await _logger.WarningAsync(errMsg);
            _notificationService.ErrorNotification(errMsg);
            return RedirectToRoute("ShoppingCart");
        }

        // Calculate order-level discount amount (e.g. coupon codes, order total discounts)
        var (_, orderTotalDiscount, _, _, _, _) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart);

        await _logger.InformationAsync($"Calling Shopify Admin API draftOrderCreate with {draftItems.Count} line items and {orderTotalDiscount:C} order discount...");
        var (success, checkoutUrl, draftMsg) = await _adminApiService.CreateDraftOrderAsync(draftItems, customer.Email, orderTotalDiscount);

        if (!success || string.IsNullOrWhiteSpace(checkoutUrl))
        {
            await _logger.ErrorAsync($"Shopify Draft Order Error: {draftMsg}");
            _notificationService.ErrorNotification($"Shopify Checkout Error: {draftMsg}");
            return RedirectToRoute("ShoppingCart");
        }

        if (!string.IsNullOrWhiteSpace(checkoutUrl))
        {
            try
            {
                var returnUrl = Url.RouteUrl("ShoppingCart", null, Request.Scheme);
                if (!string.IsNullOrWhiteSpace(returnUrl))
                {
                    var sep = checkoutUrl.Contains("?") ? "&" : "?";
                    checkoutUrl = $"{checkoutUrl}{sep}return_to={Uri.EscapeDataString(returnUrl)}";
                }
            }
            catch
            {
                // Ignore if Request.Scheme is unavailable
            }
        }

        await _logger.InformationAsync($"Shopify Draft Order created successfully. Redirecting customer to {checkoutUrl}");

        // Preserve local nopCommerce cart so items are not lost if customer abandons or navigates back
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
