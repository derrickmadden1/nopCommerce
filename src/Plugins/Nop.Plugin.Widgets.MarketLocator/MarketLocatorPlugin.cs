using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.Widgets.MarketLocator.Components;
using Nop.Plugin.Widgets.MarketLocator.Services;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Pickup;
using Nop.Services.Shipping.Tracking;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.MarketLocator;

public class MarketLocatorPlugin : BasePlugin, IWidgetPlugin, IPickupPointProvider
{
    private readonly ISettingService _settingService;
    private readonly ILocalizationService _localizationService;
    private readonly IWebHelper _webHelper;
    private readonly IMarketLocationService _locationService;

    public MarketLocatorPlugin(
        ISettingService settingService,
        ILocalizationService localizationService,
        IWebHelper webHelper,
        IMarketLocationService locationService)
    {
        _settingService = settingService;
        _localizationService = localizationService;
        _webHelper = webHelper;
        _locationService = locationService;
    }

    public bool HideInWidgetList => false;

    public Type GetWidgetViewComponent(string widgetZone)
    {
        if (widgetZone.Equals(PublicWidgetZones.HomepageTop, StringComparison.InvariantCultureIgnoreCase))
            return typeof(MarketTeaserViewComponent);

        if (widgetZone.Equals(PublicWidgetZones.CheckoutShippingAddressTop, StringComparison.InvariantCultureIgnoreCase))
            return typeof(MarketPickupSelectorViewComponent);

        if (widgetZone.Equals(PublicWidgetZones.OrderSummaryContentAfter, StringComparison.InvariantCultureIgnoreCase))
            return typeof(MarketPickupSummaryViewComponent);

        if (widgetZone.Equals("admin_order_details_info_right", StringComparison.InvariantCultureIgnoreCase))
            return typeof(MarketPickupSummaryViewComponent);

        return typeof(MarketTeaserViewComponent);
    }

    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string>
        {
            PublicWidgetZones.HomepageTop,
            PublicWidgetZones.CheckoutShippingAddressTop,
            PublicWidgetZones.OrderSummaryContentAfter,
            "admin_order_details_info_right",
        });
    }

    // ── IPickupPointProvider ────────────────────────────────────────

    public async Task<GetPickupPointsResponse> GetPickupPointsAsync(IList<ShoppingCartItem> cart, Address address)
    {
        var response = new GetPickupPointsResponse();

        // Only offer pickup if there are published markets with upcoming dates.
        var markets = await _locationService.GetAllAsync(showUnpublished: false);
        var activeMarkets = markets
            .Where(m => MarketDateHelper.HasFutureDates(m.UpcomingDates))
            .ToList();

        if (!activeMarkets.Any())
            return response;

        foreach (var market in activeMarkets)
        {
            response.PickupPoints.Add(new PickupPoint
            {
                Id = market.Id.ToString(),
                Name = market.Name,
                Description = market.Hours,
                Address = market.Address,
                City = market.City,
                Latitude = market.Latitude,
                Longitude = market.Longitude,
                PickupFee = 0m,
                ProviderSystemName = "Widgets.MarketLocator"
            });
        }

        return response;
    }

    public Task<IShipmentTracker> GetShipmentTrackerAsync() =>
        Task.FromResult<IShipmentTracker>(null!);

    // ── BasePlugin ────────────────────────────────────────────────────────────

    public override string GetConfigurationPageUrl() =>

        $"{_webHelper.GetStoreLocation()}Admin/MarketLocatorAdmin/Configure";

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new MarketLocatorSettings
        {
            DefaultLatitude  = 44.977m,
            DefaultLongitude = -93.265m,
            DefaultZoom      = 11,
            ShowTeaserWidget = true,
            TeaserMaxItems   = 2,
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            // Map plugin
            ["Plugins.Widgets.MarketLocator.Fields.Name"]           = "Market Name",
            ["Plugins.Widgets.MarketLocator.Fields.Address"]        = "Address",
            ["Plugins.Widgets.MarketLocator.Fields.City"]           = "City / Area",
            ["Plugins.Widgets.MarketLocator.Fields.Latitude"]       = "Latitude",
            ["Plugins.Widgets.MarketLocator.Fields.Longitude"]      = "Longitude",
            ["Plugins.Widgets.MarketLocator.Fields.Hours"]          = "Operating Hours",
            ["Plugins.Widgets.MarketLocator.Fields.UpcomingDates"]  = "Upcoming Dates (one per line)",
            ["Plugins.Widgets.MarketLocator.Fields.Frequency"]      = "Frequency",
            ["Plugins.Widgets.MarketLocator.Fields.Published"]      = "Published",
            ["Plugins.Widgets.MarketLocator.Fields.DisplayOrder"]   = "Display Order",
            ["Plugins.Widgets.MarketLocator.Settings.AzureMapsKey"] = "Azure Maps Subscription Key",
            ["Plugins.Widgets.MarketLocator.Settings.DefaultZoom"]  = "Default Map Zoom (1–18)",
            ["Plugins.Widgets.MarketLocator.Settings.DefaultLat"]   = "Default Map Centre — Latitude",
            ["Plugins.Widgets.MarketLocator.Settings.DefaultLng"]   = "Default Map Centre — Longitude",
            ["Plugins.Widgets.MarketLocator.Settings.ShowTeaserWidget"] = "Show homepage teaser widget",
            ["Plugins.Widgets.MarketLocator.Settings.TeaserMaxItems"]   = "Teaser: max markets to show (1–5)",
            // Pickup shipping
            ["Plugins.Shipping.MarketPickup.Name"]        = "Market Pickup",
            ["Plugins.Shipping.MarketPickup.Description"] = "Collect at one of our markets — free.",
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<MarketLocatorSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Widgets.MarketLocator");
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.MarketPickup");
        await base.UninstallAsync();
    }
}
