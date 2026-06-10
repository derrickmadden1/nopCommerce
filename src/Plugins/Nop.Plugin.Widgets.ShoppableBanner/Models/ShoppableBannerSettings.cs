using Nop.Core.Configuration;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Widgets.ShoppableBanner.Models
{
    public class ShoppableBannerSettings : ISettings
    {
        private List<HotspotRecord> _hotspots;

        public ShoppableBannerSettings()
        {
            _hotspots = new List<HotspotRecord>();
        }

        // The background image for the desktop view
        public int BackgroundPictureId { get; set; }

        public string BackgroundPictureUrl { get; set; }

        // The main heading text
        public string HeroTitle { get; set; }

        // The subtext or dynamic chalkboard text
        public string SubText { get; set; }

        public string HotspotsJson
        {
            get
            {
                if (_hotspots == null)
                    return "[]";
                try
                {
                    return System.Text.Json.JsonSerializer.Serialize(_hotspots);
                }
                catch
                {
                    return "[]";
                }
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _hotspots = new List<HotspotRecord>();
                }
                else
                {
                    try
                    {
                        _hotspots = System.Text.Json.JsonSerializer.Deserialize<List<HotspotRecord>>(value) ?? new List<HotspotRecord>();
                    }
                    catch
                    {
                        _hotspots = new List<HotspotRecord>();
                    }
                }
            }
        }

        [JsonIgnore]
        public List<HotspotRecord> Hotspots
        {
            get
            {
                if (_hotspots == null)
                {
                    _hotspots = new List<HotspotRecord>();
                }
                return _hotspots;
            }
            set
            {
                _hotspots = value ?? new List<HotspotRecord>();
            }
        }
    }

    public class HotspotRecord
    {
        public int ProductId { get; set; }

        // Using decimals for percentages (e.g., 45.5 for 45.5%)
        public decimal PositionX { get; set; }
        public decimal PositionY { get; set; }
    }
}