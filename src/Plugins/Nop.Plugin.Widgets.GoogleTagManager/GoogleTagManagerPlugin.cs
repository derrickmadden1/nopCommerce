using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.GoogleTagManager
{
    public class GoogleTagManagerPlugin : BasePlugin, IWidgetPlugin
    {
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;

        public GoogleTagManagerPlugin(ILocalizationService localizationService,
            ISettingService settingService,
            IWebHelper webHelper)
        {
            _localizationService = localizationService;
            _settingService = settingService;
            _webHelper = webHelper;
        }

        public bool HideInWidgetList => false;

        public override string GetConfigurationPageUrl()
        {
            return _webHelper.GetStoreLocation() + "Admin/WidgetsGoogleTagManager/Configure";
        }

        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>(new List<string>
            {
                PublicWidgetZones.HeadHtmlTag,
                PublicWidgetZones.BodyStartHtmlTagAfter
            });
        }

        public Type GetWidgetViewComponent(string widgetZone)
        {
            return typeof(Components.WidgetsGoogleTagManagerViewComponent);
        }

        public override async Task InstallAsync()
        {
            await _settingService.SaveSettingAsync(new GoogleTagManagerSettings
            {
                TrackingId = ""
            });

            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Widgets.GoogleTagManager.TrackingId"] = "Google Tag Manager ID",
                ["Plugins.Widgets.GoogleTagManager.TrackingId.Hint"] = "Enter your Google Tag Manager ID (e.g., GTM-XXXXXXX)."
            });

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            await _settingService.DeleteSettingAsync<GoogleTagManagerSettings>();

            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Widgets.GoogleTagManager");

            await base.UninstallAsync();
        }
    }
}
