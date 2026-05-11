using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.MarketLocator;

public class MarketLocatorSettings : ISettings
{
    /// <summary>Azure Maps subscription key. Leave empty to use free CartoDB tiles.</summary>
    public string AzureMapsKey { get; set; } = string.Empty;

    public int DefaultZoom { get; set; } = 11;
    public decimal DefaultLatitude { get; set; } = 44.97m;
    public decimal DefaultLongitude { get; set; } = -93.22m;

    /// <summary>Show the "Next Markets" teaser on the homepage widget zone.</summary>
    public bool ShowTeaserWidget { get; set; } = true;

    /// <summary>How many markets to show in the homepage teaser (1–5).</summary>
    public int TeaserMaxItems { get; set; } = 2;
    
    /// <summary>
    /// Master switch — allows admins to disable social publishing without redeploying
    /// </summary>
    public bool EnableSocialPublishing { get; set; }

    public string StoreUrl { get; set; } = string.Empty;

    /// <summary>
    /// How many days before the market start date to post on social media.
    /// Default: 3
    /// </summary>
    public int SocialPublishDaysBeforeMarket { get; set; } = 3;
}
