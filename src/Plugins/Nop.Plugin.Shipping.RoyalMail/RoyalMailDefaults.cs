namespace Nop.Plugin.Shipping.RoyalMail;

/// <summary>
/// Represents plugin constants
/// </summary>
public static class RoyalMailDefaults
{
    /// <summary>
    /// Gets the system name
    /// </summary>
    public static string SystemName => "Shipping.RoyalMail";

    /// <summary>
    /// Gets the user agent for HTTP requests
    /// </summary>
    public static string UserAgent => "nopCommerce-RoyalMail-Plugin";

    /// <summary>
    /// Gets the web tracking URL pattern
    /// </summary>
    public static string WebTrackingUrlFormat => "https://www.royalmail.com/track-your-item#/tracking-results/{0}";

    /// <summary>
    /// Gets production base URL for Royal Mail API
    /// </summary>
    public static string ProductionApiBaseUrl => "https://api.royalmail.net/";

    /// <summary>
    /// Gets sandbox base URL for Royal Mail API
    /// </summary>
    public static string SandboxApiBaseUrl => "https://sandbox.api.royalmail.net/";
}
