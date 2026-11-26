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
    private readonly RoyalMailSettings _royalMailSettings;

    #endregion

    #region Ctor

    public RoyalMailComputationMethod(ILocalizationService localizationService,
        ISettingService settingService,
        IWebHelper webHelper,
        RoyalMailSettings royalMailSettings)
    {
        _localizationService = localizationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _royalMailSettings = royalMailSettings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets available shipping options
    /// </summary>
    public Task<GetShippingOptionResponse> GetShippingOptionsAsync(GetShippingOptionRequest getShippingOptionRequest)
    {
        ArgumentNullException.ThrowIfNull(getShippingOptionRequest);
        return Task.FromResult(new GetShippingOptionResponse());
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
    public Task<IShipmentTracker> GetShipmentTrackerAsync()
    {
        return Task.FromResult<IShipmentTracker>(new RoyalMailShipmentTracker(_royalMailSettings));
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
        var settings = new RoyalMailSettings
        {
            WebTrackingUrlFormat = RoyalMailDefaults.WebTrackingUrlFormat
        };
        await _settingService.SaveSettingAsync(settings);

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Shipping.RoyalMail.Fields.WebTrackingUrlFormat"] = "Web Tracking URL Format",
            ["Plugins.Shipping.RoyalMail.Fields.WebTrackingUrlFormat.Hint"] = "The URL pattern used to redirect to Royal Mail's public tracking page. {0} will be replaced with the sanitized tracking number."
        });

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall plugin
    /// </summary>
    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<RoyalMailSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Shipping.RoyalMail");

        await base.UninstallAsync();
    }

    #endregion
}
