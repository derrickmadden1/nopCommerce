using System.Collections.Generic;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.ShoppableBanner.Models
{
    public record ShoppableBannerModel : BaseNopModel
    {
        public ShoppableBannerModel()
        {
            Hotspots = new List<HotspotModel>();
        }

        public string BackgroundPictureUrl { get; set; }
        public string HeroTitle { get; set; }
        public string SubText { get; set; }
        public IList<HotspotModel> Hotspots { get; set; }
    }

    public record HotspotModel : BaseNopModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal PositionX { get; set; }
        public decimal PositionY { get; set; }
        public string PictureUrl { get; set; }
        public string Price { get; set; }
    }

    public record QuickViewProductModel : BaseNopModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ShortDescription { get; set; }
        public string Price { get; set; }
        public string PictureUrl { get; set; }
        public string FullDescription { get; set; }
        public string Sku { get; set; }
    }
}
