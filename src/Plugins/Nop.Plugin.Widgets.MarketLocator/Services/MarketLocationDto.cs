namespace Nop.Plugin.Widgets.MarketLocator.Services;

/// <summary>
/// Lightweight read model serialised to JSON for the public map.
/// Kept flat so the frontend needs zero transformation.
/// </summary>
public class MarketLocationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Hours { get; set; } = string.Empty;

    /// <summary>Parsed from pipe-delimited UpcomingDates.</summary>
    public List<string> Dates { get; set; } = new();

    public string Frequency { get; set; } = string.Empty;

    /// <summary>"today" | "soon" | "upcoming" — calculated at query time.</summary>
    public string Status { get; set; } = "upcoming";
}
