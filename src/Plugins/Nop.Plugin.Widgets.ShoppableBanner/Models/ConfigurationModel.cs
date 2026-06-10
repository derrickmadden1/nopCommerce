using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Widgets.ShoppableBanner.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.ShoppableBanner.Fields.HeroTitle")]
        public string HeroTitle { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.ShoppableBanner.Fields.SubText")]
        public string SubText { get; set; }

        [UIHint("Picture")]
        [NopResourceDisplayName("Plugins.Widgets.ShoppableBanner.Fields.BackgroundPictureId")]
        public int BackgroundPictureId { get; set; }

        // We will pass the picture URL to the view so we can display it for the coordinate picker       
        public string BackgroundPictureUrl { get; set; }

        public HotspotSearchModel HotspotSearchModel { get; set; } = new HotspotSearchModel();
    }

    public record HotspotSearchModel : BaseSearchModel
    {
    }

    public record HotspotListModel : BaseNopEntityModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal PositionX { get; set; }
        public decimal PositionY { get; set; }
    }

    public record HotspotPagedListModel : BasePagedListModel<HotspotListModel>
    {
    }
}