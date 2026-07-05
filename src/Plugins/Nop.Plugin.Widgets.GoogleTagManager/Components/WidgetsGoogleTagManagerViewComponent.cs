using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.GoogleTagManager.Components
{
    [ViewComponent(Name = "WidgetsGoogleTagManager")]
    public class WidgetsGoogleTagManagerViewComponent : NopViewComponent
    {
        private readonly GoogleTagManagerSettings _googleTagManagerSettings;

        public WidgetsGoogleTagManagerViewComponent(GoogleTagManagerSettings googleTagManagerSettings)
        {
            _googleTagManagerSettings = googleTagManagerSettings;
        }

        public IViewComponentResult Invoke(string widgetZone, object additionalData)
        {
            if (string.IsNullOrEmpty(_googleTagManagerSettings.TrackingId))
                return Content("");

            var model = new Nop.Plugin.Widgets.GoogleTagManager.Models.PublicInfoModel
            {
                TrackingId = _googleTagManagerSettings.TrackingId,
                WidgetZone = widgetZone
            };

            return View("~/Plugins/Widgets.GoogleTagManager/Views/PublicInfo.cshtml", model);
        }
    }
}
