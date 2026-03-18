using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Widgets.MarketLocator.Models;
using Nop.Plugin.Widgets.MarketLocator.Services;

namespace Nop.Plugin.Widgets.MarketLocator.Controllers;

public partial class MarketLocatorController
{
    /// <summary>
    /// POST /market-pickup/select
    /// Called via AJAX from the checkout pickup selector widget.
    /// Saves the customer's market + date choice to their session attributes
    /// so it survives navigation between checkout steps.
    /// Returns JSON { ok: true } or { ok: false, error: "..." }.
    /// </summary>
    [HttpPost]
    [Route("market-pickup/select")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePickupSelection(
        [FromBody] MarketPickupSelectionModel model,
        [FromServices] IMarketPickupService pickupService,
        [FromServices] IWorkContext workContext,
        [FromServices] IMarketLocationService locationService)
    {
        if (model is null || model.MarketId <= 0 || string.IsNullOrWhiteSpace(model.PickupDate))
            return Json(new { ok = false, error = "Please select a market and a pickup date." });

        // Validate the market + date actually exist (prevents tampering).
        var market = await locationService.GetByIdAsync(model.MarketId);
        if (market is null || !market.Published)
            return Json(new { ok = false, error = "Selected market is not available." });

        var validDates = market.UpcomingDates
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim());

        if (!validDates.Contains(model.PickupDate))
            return Json(new { ok = false, error = "Selected date is not available for this market." });

        var customer = await workContext.GetCurrentCustomerAsync();
        await pickupService.SaveSelectionAsync(customer, model.MarketId, model.PickupDate);

        return Json(new { ok = true });
    }
}
