using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.MarketLocator.Models;
using Nop.Plugin.Widgets.MarketLocator.Services;
using Nop.Services.Configuration;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.MarketLocator.Components;

/// <summary>
/// Renders the compact "Find Us at a Market" teaser bar injected into
/// the homepage_top or home_page_before_products widget zone.
/// Controlled by MarketLocatorSettings.ShowTeaserWidget.
/// </summary>
public class MarketTeaserViewComponent : NopViewComponent
{
    private readonly IMarketLocationService _locationService;
    private readonly ISettingService _settingService;

    public MarketTeaserViewComponent(
        IMarketLocationService locationService,
        ISettingService settingService)
    {
        _locationService = locationService;
        _settingService = settingService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        var settings = await _settingService.LoadSettingAsync<MarketLocatorSettings>();

        if (!settings.ShowTeaserWidget)
            return Content(string.Empty);

        var dtos = await _locationService.GetPublishedDtosAsync();

        // Show markets with status "today" or "soon" first, then "upcoming"
        var allItems = dtos
            .OrderBy(d => d.Status switch { "today" => 0, "soon" => 1, _ => 2 })
            .Select(d => new MarketTeaserModel.TeaserItem
            {
                Id = d.Id,
                Name = d.Name,
                NextDate = d.Dates.FirstOrDefault() ?? string.Empty,
                Dates = d.Dates,
                Hours = d.Hours,
                City = d.City,
                Status = d.Status,
            })
            .ToList();

        if (!allItems.Any())
            return Content(string.Empty);

        var model = new MarketTeaserModel
        {
            TotalCount = allItems.Count,
            NextMarkets = allItems.Take(settings.TeaserMaxItems).ToList(),
            AllMarkets = allItems,
        };

        return View("~/Plugins/Widgets.MarketLocator/Views/MarketLocator/Teaser.cshtml", model);
    }
}
