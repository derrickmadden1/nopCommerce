using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Plugin.Widgets.ShoppableBanner.Models;
using Nop.Services.Cms;
using Nop.Services.Helpers;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.ShoppableBanner
{
    public class ShoppableBannerPlugin : BasePlugin, IWidgetPlugin
    {
        #region Fields

        protected readonly ILocalizationService _localizationService;
        protected readonly ISettingService _settingService;
        protected readonly WidgetSettings _widgetSettings;
        protected readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public ShoppableBannerPlugin(
            ILocalizationService localizationService,
            ISettingService settingService,
            WidgetSettings widgetSettings,
            IWebHelper webHelper)
        {
            _localizationService = localizationService;
            _settingService = settingService;
            _widgetSettings = widgetSettings;
            _webHelper = webHelper;
        }

        #endregion

        #region Methods

        // Define which widget zone(s) the banner should appear in
        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>(new List<string>
            {
                PublicWidgetZones.HomepageTop // Injects right at the top of the home page
            });
        }

        // Point to the View Component we will create
        public Type GetWidgetViewComponent(string widgetZone)
        {
            return typeof(Components.WidgetsShoppableBannerViewComponent);
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/WidgetsShoppableBanner/Configure";
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            // Settings
            var settings = new ShoppableBannerSettings
            {
                HeroTitle = "Shop the Room",
                SubText = "Hover over hotspots to explore our natural bar soaps and balms.",
                MobileSubText = "Explore our natural bar soaps and balms.",
                BackgroundPictureId = 0
            };
            await _settingService.SaveSettingAsync(settings);

            // Activate widget
            if (!_widgetSettings.ActiveWidgetSystemNames.Contains(PluginDescriptor.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Add(PluginDescriptor.SystemName);
                await _settingService.SaveSettingAsync(_widgetSettings);
            }

            // Locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Widgets.ShoppableBanner.Fields.HeroTitle"] = "Hero Title",
                ["Plugins.Widgets.ShoppableBanner.Fields.HeroTitle.Hint"] = "Enter the main heading text for the shoppable banner.",
                ["Plugins.Widgets.ShoppableBanner.Fields.SubText"] = "Subtext",
                ["Plugins.Widgets.ShoppableBanner.Fields.SubText.Hint"] = "Enter subtitle or chalkboard/promotional description text.",
                ["Plugins.Widgets.ShoppableBanner.Fields.MobileSubText"] = "Mobile Subtext",
                ["Plugins.Widgets.ShoppableBanner.Fields.MobileSubText.Hint"] = "Enter subtitle description text specifically for mobile devices (without 'hover over hotspots' phrasing).",
                ["Plugins.Widgets.ShoppableBanner.Fields.BackgroundPictureId"] = "Background Image",
                ["Plugins.Widgets.ShoppableBanner.Fields.BackgroundPictureId.Hint"] = "Upload the background image to place product hotspots onto."
            });

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            // Settings
            await _settingService.DeleteSettingAsync<ShoppableBannerSettings>();
            
            if (_widgetSettings.ActiveWidgetSystemNames.Contains(PluginDescriptor.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Remove(PluginDescriptor.SystemName);
                await _settingService.SaveSettingAsync(_widgetSettings);
            }

            // Locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Widgets.ShoppableBanner");

            await base.UninstallAsync();
        }

        #endregion

        #region Properties

        public bool HideInWidgetList => false;

        #endregion
    }
}