using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.ShopifyCheckout.Models;
using Nop.Services.Security;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Components;

[ViewComponent(Name = "WidgetsShopifyCheckout")]
public class ShopifyCheckoutViewComponent : NopViewComponent
{
    #region Fields

    private readonly ShopifyCheckoutSettings _settings;
    private readonly IPermissionService _permissionService;

    #endregion

    #region Ctor

    public ShopifyCheckoutViewComponent(
        ShopifyCheckoutSettings settings,
        IPermissionService permissionService)
    {
        _settings = settings;
        _permissionService = permissionService;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (widgetZone.Equals(PublicWidgetZones.OrderSummaryTotals, StringComparison.OrdinalIgnoreCase))
        {
            if (!_settings.DisplayButtonOnShoppingCart)
                return Content(string.Empty);
        }
        else if (widgetZone.Equals(PublicWidgetZones.CheckoutPaymentMethodTop, StringComparison.OrdinalIgnoreCase))
        {
            if (!_settings.DisplayButtonOnPaymentMethod)
                return Content(string.Empty);
        }
        else
        {
            return Content(string.Empty);
        }

        bool isConfigured = !string.IsNullOrWhiteSpace(_settings.StoreUrl) && !string.IsNullOrWhiteSpace(_settings.StorefrontAccessToken);
        bool isAdmin = await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_WIDGETS);

        if (!isConfigured && !isAdmin)
        {
            // Do not display unconfigured button to regular store customers
            return Content(string.Empty);
        }

        var model = new ShopifyCheckoutButtonModel
        {
            ButtonText = string.IsNullOrWhiteSpace(_settings.CustomButtonText) ? "Checkout with Shopify" : _settings.CustomButtonText,
            InitCheckoutUrl = Url.Action("InitCheckout", "ShopifyCheckout"),
            IsConfigured = isConfigured,
            IsAdmin = isAdmin
        };

        return View("~/Plugins/Widgets.ShopifyCheckout/Views/PublicInfo.cshtml", model);
    }

    #endregion
}
