using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.Widgets.MarketLocator.Services;
using static Nop.Plugin.Widgets.MarketLocator.Services.MarketDateHelper;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Tracking;

namespace Nop.Plugin.Widgets.MarketLocator.Shipping;

/// <summary>
/// Exposes "Market Pickup" as a shipping option in the nopCommerce checkout.
/// Returns a single £0 / $0 rate for every published market location.
/// The customer selects their preferred location + date via the checkout widget
/// (MarketPickupShippingWidget) injected into the shipping_method_buttons zone.
/// </summary>
public class MarketPickupShippingPlugin : BasePlugin, IShippingRateComputationPlugin
{
    public const string SystemName = "Shipping.MarketPickup";
    private const string ShippingOptionName = "Market Pickup";

    private readonly IMarketLocationService _locationService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IWorkContext _workContext;
    private readonly ISettingService _settingService;
    private readonly ILocalizationService _localizationService;
    private readonly IWebHelper _webHelper;

    public MarketPickupShippingPlugin(
        IMarketLocationService locationService,
        IGenericAttributeService genericAttributeService,
        IWorkContext workContext,
        ISettingService settingService,
        ILocalizationService localizationService,
        IWebHelper webHelper)
    {
        _locationService = locationService;
        _genericAttributeService = genericAttributeService;
        _workContext = workContext;
        _settingService = settingService;
        _localizationService = localizationService;
        _webHelper = webHelper;
    }

    // ── IShippingRateComputationPlugin ────────────────────────────────────────

    public async Task<GetShippingOptionResponse> GetShippingOptionsAsync(
        GetShippingOptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = new GetShippingOptionResponse();

        // Only offer pickup if there are published markets with upcoming dates.
        var markets = await _locationService.GetAllAsync(showUnpublished: false);
        var activeMarkets = markets
            .Where(m => MarketDateHelper.HasFutureDates(m.UpcomingDates))
            .ToList();

        if (!activeMarkets.Any())
            return response; // No markets active — option simply won't appear

        response.ShippingOptions.Add(new ShippingOption
        {
            Name           = ShippingOptionName,
            Description    = "Collect your order at one of our upcoming markets — free.",
            Rate           = 0m,
            TransitDays    = null,
            IsPickupInStore = true,
            ShippingRateComputationMethodSystemName = SystemName,
        });

        return response;
    }

    public Task<decimal> GetFixedRateAsync(GetShippingOptionRequest request) =>
        Task.FromResult(decimal.Zero);

    public Task<IShipmentTracker?> GetTrackingNumberAsync(string trackingNumber) =>
        Task.FromResult<IShipmentTracker?>(null);

    public bool HideShipmentMethods(IList<ShoppingCartItem> cart) => false;

    public bool SkipShippingInfo => false;

    // ── BasePlugin ────────────────────────────────────────────────────────────

    public override string GetConfigurationPageUrl() =>
        $"{_webHelper.GetStoreLocation()}Admin/MarketLocatorAdmin/Configure";

    public override async Task InstallAsync()
    {
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Shipping.MarketPickup.Name"]        = "Market Pickup",
            ["Plugins.Shipping.MarketPickup.Description"] = "Collect at one of our farmers markets — free.",
            ["Plugins.Shipping.MarketPickup.SelectPrompt"]= "Choose a market and date for pickup:",
            ["Plugins.Shipping.MarketPickup.NoSelection"] = "Please select a market location and pickup date.",
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.MarketPickup");
        await base.UninstallAsync();
    }
}
