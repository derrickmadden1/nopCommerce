using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Widgets.MarketLocator.Services;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.MarketLocator.Components;

/// <summary>
/// Injected into <c>order_summary_content_after</c> (public) and
/// <c>admin_order_details_info_right</c> (admin) widget zones.
/// Silently renders nothing for non-pickup orders.
/// </summary>
public class MarketPickupSummaryViewComponent : NopViewComponent
{
    private readonly IMarketPickupService _pickupService;

    public MarketPickupSummaryViewComponent(IMarketPickupService pickupService)
    {
        _pickupService = pickupService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        // additionalData is the order Id (int) passed by nopCommerce widget infrastructure.
        if (additionalData is not int orderId || orderId <= 0)
            return Content(string.Empty);

        // We need the Order entity — construct a lightweight stub so we can
        // call GetAttributeAsync without loading the full order graph.
        var order = new Order { Id = orderId };
        var summary = await _pickupService.GetOrderSummaryAsync(order);

        if (!summary.HasPickup)
            return Content(string.Empty);

        // Choose template based on zone.
        var view = widgetZone.Contains("admin")
            ? "~/Plugins/Widgets.MarketLocator/Views/Admin/OrderPickup/Summary.cshtml"
            : "~/Plugins/Widgets.MarketLocator/Views/Checkout/OrderPickupSummary.cshtml";

        return View(view, summary);
    }
}
