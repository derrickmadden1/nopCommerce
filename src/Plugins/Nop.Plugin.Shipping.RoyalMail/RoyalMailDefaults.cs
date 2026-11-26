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
    /// Gets the default web tracking URL pattern
    /// </summary>
    public static string WebTrackingUrlFormat => "https://www.royalmail.com/track-your-item#/tracking-results/{0}";
}
