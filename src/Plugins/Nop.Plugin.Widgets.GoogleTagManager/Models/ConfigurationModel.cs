using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Widgets.GoogleTagManager.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Widgets.GoogleTagManager.TrackingId")]
        public string TrackingId { get; set; }
        public bool TrackingId_OverrideForStore { get; set; }
    }
}
