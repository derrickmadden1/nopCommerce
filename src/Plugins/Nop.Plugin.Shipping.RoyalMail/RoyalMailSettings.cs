using Nop.Core.Configuration;

namespace Nop.Plugin.Shipping.RoyalMail;

/// <summary>
/// Represents settings of the Royal Mail shipping plugin
/// </summary>
public class RoyalMailSettings : ISettings
{
    /// <summary>
    /// Gets or sets the client ID (API key)
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use sandbox environment
    /// </summary>
    public bool UseSandbox { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use web tracking URL fallback when API returns no events
    /// </summary>
    public bool UseWebTrackingUrlFallback { get; set; } = true;
}
