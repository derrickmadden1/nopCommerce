using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Widgets.GoogleTagManager.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.GoogleTagManager.Controllers
{
    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    public class WidgetsGoogleTagManagerController : BasePluginController
    {
        private readonly GoogleTagManagerSettings _googleTagManagerSettings;
        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;

        public WidgetsGoogleTagManagerController(GoogleTagManagerSettings googleTagManagerSettings,
            ILocalizationService localizationService,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService)
        {
            _googleTagManagerSettings = googleTagManagerSettings;
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
        }

        [CheckPermission(StandardPermission.Configuration.MANAGE_WIDGETS)]
        public IActionResult Configure()
        {
            var model = new ConfigurationModel
            {
                TrackingId = _googleTagManagerSettings.TrackingId
            };

            return View("~/Plugins/Widgets.GoogleTagManager/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [CheckPermission(StandardPermission.Configuration.MANAGE_WIDGETS)]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            _googleTagManagerSettings.TrackingId = model.TrackingId;
            await _settingService.SaveSettingAsync(_googleTagManagerSettings);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return Configure();
        }
    }
}
