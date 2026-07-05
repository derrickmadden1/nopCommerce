using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.GoogleTagManager.Models
{
    public record PublicInfoModel : BaseNopModel
    {
        public string TrackingId { get; set; }
        public string WidgetZone { get; set; }
    }
}
