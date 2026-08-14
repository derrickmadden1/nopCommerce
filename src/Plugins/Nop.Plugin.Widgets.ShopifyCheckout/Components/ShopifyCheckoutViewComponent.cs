using System;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Http;
using Nop.Plugin.Widgets.ShopifyCheckout.Models;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Components;

[ViewComponent(Name = "WidgetsShopifyCheckout")]
public class ShopifyCheckoutViewComponent : NopViewComponent
{
    #region Fields

    private readonly ShopifyCheckoutSettings _settings;

    #endregion

    #region Ctor

    public ShopifyCheckoutViewComponent(ShopifyCheckoutSettings settings)
    {
        _settings = settings;
    }

    #endregion

    #region Methods

    public IViewComponentResult Invoke(string widgetZone, object additionalData)
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

        var model = new ShopifyCheckoutButtonModel
        {
            ButtonText = string.IsNullOrWhiteSpace(_settings.CustomButtonText) ? "Checkout with Shopify" : _settings.CustomButtonText,
            InitCheckoutUrl = Url.Action("InitCheckout", "ShopifyCheckout")
        };

        return View("~/Plugins/Widgets.ShopifyCheckout/Views/PublicInfo.cshtml", model);
    }

    #endregion
}
