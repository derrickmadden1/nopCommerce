using Nop.Core;

namespace Nop.Plugin.Widgets.MarketLocator.Domain;

/// <summary>
/// Represents a single market location with recurrence dates.
/// </summary>
public class MarketLocation : BaseEntity
{
    /// <summary>Display name shown on the map popup and sidebar card.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Street address (shown in popup and used for "Get Directions").</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>City / region label shown on the card.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Latitude coordinate (WGS-84).</summary>
    public decimal Latitude { get; set; }

    /// <summary>Longitude coordinate (WGS-84).</summary>
    public decimal Longitude { get; set; }

    /// <summary>Operating hours description, e.g. "8:00 AM – 1:00 PM".</summary>
    public string Hours { get; set; } = string.Empty;

    /// <summary>
    /// Pipe-delimited upcoming dates, e.g. "Sat, Mar 14|Sat, Mar 21".
    /// Kept simple intentionally — swap for a child table if dates grow complex.
    /// </summary>
    public string UpcomingDates { get; set; } = string.Empty;

    /// <summary>Weekly, Bi-weekly, Monthly, etc. Shown as a filter tag.</summary>
    public string Frequency { get; set; } = "Weekly";

    /// <summary>today | soon | upcoming — controls badge colour. Auto-calculated by service.</summary>
    public string Status { get; set; } = "upcoming";

    /// <summary>False = hidden from public map without being deleted.</summary>
    public bool Published { get; set; } = true;

    /// <summary>Controls sidebar sort order.</summary>
    public int DisplayOrder { get; set; }
}
