using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Widgets.MarketLocator.Models;

// ── Admin list ──────────────────────────────────────────────────────────────

public record MarketLocationListModel : BasePagedListModel<MarketLocationModel> { }

// ── Admin create / edit ─────────────────────────────────────────────────────

public class MarketLocationModel : BaseNopEntityModel
{
    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Fields.Name")]
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Fields.Address")]
    [Required, MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Fields.City")]
    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Fields.Latitude")]
    [Required, Range(-90, 90)]
    public decimal Latitude { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Fields.Longitude")]
    [Required, Range(-180, 180)]
    public decimal Longitude { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Fields.Hours")]
    [MaxLength(100)]
    public string Hours { get; set; } = string.Empty;

    /// <summary>
    /// Newline-separated in the textarea; stored pipe-delimited in the DB.
    /// </summary>
    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Fields.UpcomingDates")]
    public string UpcomingDatesRaw { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Fields.Frequency")]
    public string Frequency { get; set; } = "Weekly";

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Fields.Published")]
    public bool Published { get; set; } = true;

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Fields.DisplayOrder")]
    public int DisplayOrder { get; set; }

    public IList<SelectListItem> AvailableFrequencies { get; set; } = new List<SelectListItem>
    {
        new("Weekly",     "Weekly"),
        new("Bi-weekly",  "Bi-weekly"),
        new("Monthly",    "Monthly"),
        new("One-time",   "One-time"),
    };
}

// ── Homepage teaser widget ──────────────────────────────────────────────────

public class MarketTeaserModel
{
    public int TotalCount { get; set; }
    public List<TeaserItem> NextMarkets { get; set; } = new();

    public class TeaserItem
    {
        public string Name { get; set; } = string.Empty;
        public string NextDate { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Status { get; set; } = "upcoming";
    }
}

// ── Settings ────────────────────────────────────────────────────────────────

public class MarketLocatorSettingsModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Settings.AzureMapsKey")]
    public string AzureMapsKey { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Settings.DefaultZoom")]
    [Range(1, 18)]
    public int DefaultZoom { get; set; } = 11;

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Settings.DefaultLat")]
    public decimal DefaultLatitude { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Settings.DefaultLng")]
    public decimal DefaultLongitude { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Settings.ShowTeaserWidget")]
    public bool ShowTeaserWidget { get; set; } = true;

    [NopResourceDisplayName("Plugins.Widgets.MarketLocator.Settings.TeaserMaxItems")]
    [Range(1, 5)]
    public int TeaserMaxItems { get; set; } = 2;
}
