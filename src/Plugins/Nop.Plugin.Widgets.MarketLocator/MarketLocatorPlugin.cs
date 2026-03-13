using Nop.Core;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.MarketLocator;

public class MarketLocatorPlugin : BasePlugin, IWidgetPlugin
{
    private readonly ISettingService _settingService;
    private readonly ILocalizationService _localizationService;
    private readonly IWebHelper _webHelper;

    public MarketLocatorPlugin(
        ISettingService settingService,
        ILocalizationService localizationService,
        IWebHelper webHelper)
    {
        _settingService = settingService;
        _localizationService = localizationService;
        _webHelper = webHelper;
    }

    public bool HideInWidgetList => false;

    public string GetWidgetViewComponentName(string widgetZone) => widgetZone switch
    {
        // Homepage teaser bar
        PublicWidgetZones.HomepageTop                  => "MarketTeaser",

        // Checkout: location + date picker (injected when Market Pickup is chosen)
        "checkout_shipping_method_buttons"             => "MarketPickupSelector",

        // Public order detail / confirmation page
        PublicWidgetZones.OrderSummaryContentAfter     => "MarketPickupSummary",

        // Admin order detail sidebar
        "admin_order_details_info_right"               => "MarketPickupSummary",

        _                                              => "MarketTeaser",
    };

    public Task<IList<string>> GetWidgetZonesAsync() =>
        Task.FromResult<IList<string>>(new List<string>
        {
            PublicWidgetZones.HomepageTop,
            "checkout_shipping_method_buttons",
            PublicWidgetZones.OrderSummaryContentAfter,
            "admin_order_details_info_right",
        });

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
            ["Plugins.Shipping.MarketPickup.Description"] = "Collect at one of our farmers markets — free.",
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
