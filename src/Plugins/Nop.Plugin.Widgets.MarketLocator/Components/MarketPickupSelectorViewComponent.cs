using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.Widgets.MarketLocator.Models;
using Nop.Plugin.Widgets.MarketLocator.Services;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.MarketLocator.Components;

public class MarketPickupSelectorViewComponent : NopViewComponent
{
    private readonly IMarketLocationService _locationService;
    private readonly IMarketPickupService _pickupService;
    private readonly IWorkContext _workContext;

    public MarketPickupSelectorViewComponent(
        IMarketLocationService locationService,
        IMarketPickupService pickupService,
        IWorkContext workContext)
    {
        _locationService = locationService;
        _pickupService = pickupService;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var (selectedMarketId, selectedDate) = await _pickupService.GetSelectionAsync(customer);

        var allLocations = await _locationService.GetAllAsync(showUnpublished: false);

        var options = allLocations
            .Select(m => new
            {
                Market = m,
                // Filter to today-or-future dates only
                FutureDates = MarketDateHelper.GetFutureDates(m.UpcomingDates),
            })
            .Where(x => x.FutureDates.Any())   // drop markets with no remaining dates
            .OrderBy(x => x.Market.DisplayOrder)
            .ThenBy(x => x.Market.Name)
            .Select(x => new MarketPickupOption
            {
                Id          = x.Market.Id,
                Name        = x.Market.Name,
                Address     = x.Market.Address,
                City        = x.Market.City,
                Hours       = x.Market.Hours,
                DateOptions = x.FutureDates
                    .Select(d => new SelectListItem(d, d))
                    .ToList(),
            })
            .ToList();

        // If the customer's saved date is no longer valid (it was in the past),
        // clear it so they are forced to re-select rather than silently submitting
        // a stale date.
        if (!string.IsNullOrWhiteSpace(selectedDate))
        {
            var savedMarket = options.FirstOrDefault(o => o.Id == selectedMarketId);
            var dateStillValid = savedMarket?.DateOptions.Any(d => d.Value == selectedDate) ?? false;
            if (!dateStillValid)
            {
                await _pickupService.ClearSelectionAsync(customer);
                selectedDate = string.Empty;
            }
        }

        var model = new MarketPickupSelectorModel
        {
            Markets                = options,
            SelectedMarketId       = selectedMarketId,
            SelectedDate           = selectedDate,
            IsMarketPickupSelected = true,
        };

        return View("~/Plugins/Widgets.MarketLocator/Views/Checkout/PickupSelector.cshtml", model);
    }
}
