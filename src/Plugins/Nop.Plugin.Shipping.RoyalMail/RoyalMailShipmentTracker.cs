using Nop.Core.Domain.Shipping;
using Nop.Services.Shipping.Tracking;

namespace Nop.Plugin.Shipping.RoyalMail;

/// <summary>
/// Represents Royal Mail shipment tracker implementing IShipmentTracker
/// </summary>
public class RoyalMailShipmentTracker : IShipmentTracker
{
    #region Fields

    private readonly RoyalMailSettings _royalMailSettings;

    #endregion

    #region Ctor

    public RoyalMailShipmentTracker(RoyalMailSettings royalMailSettings)
    {
        _royalMailSettings = royalMailSettings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets URL for third-party Royal Mail web tracking page
    /// </summary>
    /// <param name="trackingNumber">Tracking number</param>
    /// <param name="shipment">Shipment</param>
    /// <returns>Tracking URL string</returns>
    public Task<string> GetUrlAsync(string trackingNumber, Shipment shipment = null)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return Task.FromResult(string.Empty);

        // Remove any spaces (e.g. "YD 0260 6106 9GB" -> "YD026061069GB") for reliable web tracking
        var cleanTrackingNumber = trackingNumber.Replace(" ", "").Trim();

        var format = string.IsNullOrWhiteSpace(_royalMailSettings?.WebTrackingUrlFormat)
            ? RoyalMailDefaults.WebTrackingUrlFormat
            : _royalMailSettings.WebTrackingUrlFormat;

        var url = string.Format(format, Uri.EscapeDataString(cleanTrackingNumber));
        return Task.FromResult(url);
    }

    /// <summary>
    /// Gets all shipment tracking events
    /// </summary>
    /// <param name="trackingNumber">Tracking number</param>
    /// <param name="shipment">Shipment</param>
    /// <returns>List of shipment status events</returns>
    public Task<IList<ShipmentStatusEvent>> GetShipmentEventsAsync(string trackingNumber, Shipment shipment = null)
    {
        // Royal Mail Tracking API access is restricted to enterprise accounts posting 25k+ tracked items/yr.
        // Returning an empty list bypasses unsupported API calls and prevents 401 Unauthorized errors in logs.
        // Customers and store admins track shipments directly via the official web tracking link from GetUrlAsync.
        return Task.FromResult<IList<ShipmentStatusEvent>>(new List<ShipmentStatusEvent>());
    }

    #endregion
}
