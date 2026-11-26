using Nop.Core.Configuration;

namespace Nop.Plugin.Shipping.RoyalMail;

/// <summary>
/// Represents settings of the Royal Mail shipping plugin
/// </summary>
public class RoyalMailSettings : ISettings
{
    /// <summary>
    /// Gets or sets the web tracking URL pattern
    /// </summary>
    public string WebTrackingUrlFormat { get; set; } = RoyalMailDefaults.WebTrackingUrlFormat;
}
