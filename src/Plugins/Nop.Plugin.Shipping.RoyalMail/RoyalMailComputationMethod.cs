using Nop.Plugin.Shipping.RoyalMail.Services;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Tracking;

namespace Nop.Plugin.Shipping.RoyalMail;

/// <summary>
/// Represents Royal Mail shipping & tracking computation method
/// </summary>
public class RoyalMailComputationMethod : BasePlugin, IShippingRateComputationMethod
{
    #region Fields

    private readonly ILocalizationService _localizationService;
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;
    private readonly RoyalMailService _royalMailService;

    #endregion

    #region Ctor

    public RoyalMailComputationMethod(ILocalizationService localizationService,
        ISettingService settingService,
        IWebHelper webHelper,
        RoyalMailService royalMailService)
    {
        _localizationService = localizationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _royalMailService = royalMailService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets available shipping options
    /// </summary>
    /// <param name="getShippingOptionRequest">A request for getting shipping options</param>
    /// <returns>GetShippingOptionResponse</returns>
    public Task<GetShippingOptionResponse> GetShippingOptionsAsync(GetShippingOptionRequest getShippingOptionRequest)
    {
        ArgumentNullException.ThrowIfNull(getShippingOptionRequest);

        var response = new GetShippingOptionResponse();
        // Shipping rate calculation logic can be added here if desired.
        return Task.FromResult(response);
    }

    /// <summary>
    /// Gets fixed shipping rate
    /// </summary>
    public Task<decimal?> GetFixedRateAsync(GetShippingOptionRequest getShippingOptionRequest)
    {
        return Task.FromResult<decimal?>(null);
    }

    /// <summary>
    /// Gets associated shipment tracker
    /// </summary>
    /// <returns>Shipment tracker</returns>
    public Task<IShipmentTracker> GetShipmentTrackerAsync()
    {
        return Task.FromResult<IShipmentTracker>(new RoyalMailShipmentTracker(_royalMailService));
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/RoyalMailShipping/Configure";
    }

    /// <summary>
    /// Install plugin
    /// </summary>
    public override async Task InstallAsync()
    {
        // Settings default
        var settings = new RoyalMailSettings
        {
            UseSandbox = true,
            ClientId = string.Empty,
            ClientSecret = string.Empty,
            UseWebTrackingUrlFallback = true
        };
        await _settingService.SaveSettingAsync(settings);

        // Locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Shipping.RoyalMail.Fields.UseSandbox"] = "Use Sandbox",
            ["Plugins.Shipping.RoyalMail.Fields.UseSandbox.Hint"] = "Check to use Royal Mail sandbox API environment.",
            ["Plugins.Shipping.RoyalMail.Fields.ClientId"] = "Client ID (API Key)",
            ["Plugins.Shipping.RoyalMail.Fields.ClientId.Hint"] = "Enter your Royal Mail Developer Portal Client ID.",
            ["Plugins.Shipping.RoyalMail.Fields.ClientSecret"] = "Client Secret",
            ["Plugins.Shipping.RoyalMail.Fields.ClientSecret.Hint"] = "Enter your Royal Mail Developer Portal Client Secret.",
            ["Plugins.Shipping.RoyalMail.Fields.UseWebTrackingUrlFallback"] = "Enable Web Tracking Fallback",
            ["Plugins.Shipping.RoyalMail.Fields.UseWebTrackingUrlFallback.Hint"] = "Directs customers to Royal Mail's public web tracker if API events are unavailable."
        });

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall plugin
    /// </summary>
    public override async Task UninstallAsync()
    {
        // Settings
        await _settingService.DeleteSettingAsync<RoyalMailSettings>();

        // Locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.RoyalMail");

        await base.UninstallAsync();
    }

    #endregion
}
