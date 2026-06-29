using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Configuration;
using Nop.Plugin.Misc.UniversalCommerce.Domain;
using Nop.Plugin.Misc.UniversalCommerce.Models;
using Nop.Services.Messages;
using Nop.Services.Configuration;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.UniversalCommerce.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [AutoValidateAntiforgeryToken]
    public class UcpAdminController : BasePluginController
    {
        private readonly ISettingService _settingService;
        private readonly INotificationService _notificationService;
        private readonly UcpSettings _ucpSettings;

        public UcpAdminController(
            ISettingService settingService,
            INotificationService notificationService,
            UcpSettings ucpSettings)
        {
            _settingService = settingService;
            _notificationService = notificationService;
            _ucpSettings = ucpSettings;
        }

        [HttpGet]
        public IActionResult Configure()
        {
            var model = new ConfigurationModel
            {
                Enabled = _ucpSettings.Enabled,
                ProtocolVersion = _ucpSettings.ProtocolVersion,
                AllowAutonomousCheckout = _ucpSettings.AllowAutonomousCheckout,
                PermitLimit = _ucpSettings.PermitLimit,
                WindowInSeconds = _ucpSettings.WindowInSeconds,
                QueueLimit = _ucpSettings.QueueLimit
            };

            return View("~/Plugins/Misc.UniversalCommerce/Views/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid) return Configure();

            // Map UI updates back onto the persistent settings instance
            _ucpSettings.Enabled = model.Enabled;
            _ucpSettings.ProtocolVersion = model.ProtocolVersion;
            _ucpSettings.AllowAutonomousCheckout = model.AllowAutonomousCheckout;
            _ucpSettings.PermitLimit = model.PermitLimit;
            _ucpSettings.WindowInSeconds = model.WindowInSeconds;
            _ucpSettings.QueueLimit = model.QueueLimit;

            await _settingService.SaveSettingAsync(_ucpSettings);
            _notificationService.SuccessNotification("Google Universal Commerce configurations updated successfully.");

            return Configure();
        }
    }
}
