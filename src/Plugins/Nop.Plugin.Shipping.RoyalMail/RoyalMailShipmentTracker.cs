using Nop.Core.Domain.Shipping;
using Nop.Plugin.Shipping.RoyalMail.Services;
using Nop.Services.Shipping.Tracking;

namespace Nop.Plugin.Shipping.RoyalMail;

/// <summary>
/// Represents Royal Mail shipment tracker implementing IShipmentTracker
/// </summary>
public class RoyalMailShipmentTracker : IShipmentTracker
{
    #region Fields

    private readonly RoyalMailService _royalMailService;

    #endregion

    #region Ctor

    public RoyalMailShipmentTracker(RoyalMailService royalMailService)
    {
        _royalMailService = royalMailService;
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

        var url = string.Format(RoyalMailDefaults.WebTrackingUrlFormat, Uri.EscapeDataString(trackingNumber.Trim()));
        return Task.FromResult(url);
    }

    /// <summary>
    /// Gets all shipment tracking events from Royal Mail API
    /// </summary>
    /// <param name="trackingNumber">Tracking number</param>
    /// <param name="shipment">Shipment</param>
    /// <returns>List of shipment status events</returns>
    public async Task<IList<ShipmentStatusEvent>> GetShipmentEventsAsync(string trackingNumber, Shipment shipment = null)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return new List<ShipmentStatusEvent>();

        return await _royalMailService.GetShipmentEventsAsync(trackingNumber);
    }

    #endregion
}
