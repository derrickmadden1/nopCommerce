using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.ShopifyCheckout;

public class ShopifyCheckoutPlugin : BasePlugin, IWidgetPlugin
{
    #region Fields

    private readonly ILocalizationService _localizationService;
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public ShopifyCheckoutPlugin(
        ILocalizationService localizationService,
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _localizationService = localizationService;
        _settingService = settingService;
        _webHelper = webHelper;
    }

    #endregion

    #region Properties

    public bool HideInWidgetList => false;

    #endregion

    #region Methods

    public override string GetConfigurationPageUrl()
    {
        return _webHelper.GetStoreLocation() + "Admin/ShopifyCheckout/Configure";
    }

    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string>
        {
            PublicWidgetZones.OrderSummaryTotals,
            PublicWidgetZones.CheckoutPaymentMethodTop
        });
    }

    public Type GetWidgetViewComponent(string widgetZone)
    {
        return typeof(Components.ShopifyCheckoutViewComponent);
    }

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new ShopifyCheckoutSettings
        {
            StoreUrl = "",
            StorefrontAccessToken = "",
            ApiVersion = ShopifyCheckoutDefaults.DefaultApiVersion,
            DisplayButtonOnShoppingCart = true,
            DisplayButtonOnPaymentMethod = true,
            FallbackToSkuAsVariantId = true,
            CustomButtonText = "Checkout with Shopify"
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Widgets.ShopifyCheckout.Fields.StoreUrl"] = "Shopify Store URL",
            ["Plugins.Widgets.ShopifyCheckout.Fields.StoreUrl.Hint"] = "Enter your Shopify Store URL (e.g. your-store.myshopify.com).",
            ["Plugins.Widgets.ShopifyCheckout.Fields.StorefrontAccessToken"] = "Storefront Access Token",
            ["Plugins.Widgets.ShopifyCheckout.Fields.StorefrontAccessToken.Hint"] = "Enter your Shopify Storefront API Access Token.",
            ["Plugins.Widgets.ShopifyCheckout.Fields.ApiVersion"] = "API Version",
            ["Plugins.Widgets.ShopifyCheckout.Fields.ApiVersion.Hint"] = "Enter the Shopify Storefront API version (e.g. 2024-07).",
            ["Plugins.Widgets.ShopifyCheckout.Fields.DisplayButtonOnShoppingCart"] = "Display on Shopping Cart",
            ["Plugins.Widgets.ShopifyCheckout.Fields.DisplayButtonOnShoppingCart.Hint"] = "Check to display the 'Checkout with Shopify' button on the cart page.",
            ["Plugins.Widgets.ShopifyCheckout.Fields.DisplayButtonOnPaymentMethod"] = "Display on Checkout Payment Page",
            ["Plugins.Widgets.ShopifyCheckout.Fields.DisplayButtonOnPaymentMethod.Hint"] = "Check to display the button on the payment method selection page.",
            ["Plugins.Widgets.ShopifyCheckout.Fields.FallbackToSkuAsVariantId"] = "Fallback to SKU as Variant ID",
            ["Plugins.Widgets.ShopifyCheckout.Fields.FallbackToSkuAsVariantId.Hint"] = "If enabled, uses product or attribute combination SKU as Shopify Variant ID if no explicit generic attribute mapping exists.",
            ["Plugins.Widgets.ShopifyCheckout.Fields.CustomButtonText"] = "Checkout Button Text",
            ["Plugins.Widgets.ShopifyCheckout.Fields.CustomButtonText.Hint"] = "Enter custom text for the Shopify checkout button."
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<ShopifyCheckoutSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Widgets.ShopifyCheckout");
        await base.UninstallAsync();
    }

    #endregion
}
