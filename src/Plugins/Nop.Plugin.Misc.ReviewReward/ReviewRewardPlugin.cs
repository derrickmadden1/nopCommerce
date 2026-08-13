using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core.Domain.Cms;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.ReviewReward.Components;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Misc.ReviewReward
{
    public class ReviewRewardPlugin : BasePlugin, IMiscPlugin, IWidgetPlugin
    {
        private readonly IMigrationManager _migrationManager;
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly WidgetSettings _widgetSettings;

        public ReviewRewardPlugin(
            IMigrationManager migrationManager,
            ILocalizationService localizationService,
            ISettingService settingService,
            WidgetSettings widgetSettings)
        {
            _migrationManager = migrationManager;
            _localizationService = localizationService;
            _settingService = settingService;
            _widgetSettings = widgetSettings;
        }

        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>(new List<string> { PublicWidgetZones.ProductReviewsPageBottom });
        }

        public Type GetWidgetViewComponent(string widgetZone)
        {
            return typeof(ReviewRewardReviewFormViewComponent);
        }

        public bool HideInWidgetList => false;

        public override async Task InstallAsync()
        {
            _migrationManager.ApplyUpMigrations(GetType().Assembly);

            if (!_widgetSettings.ActiveWidgetSystemNames.Contains(PluginDescriptor.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Add(PluginDescriptor.SystemName);
                await _settingService.SaveSettingAsync(_widgetSettings);
            }

            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Misc.ReviewReward.MarketCode"] = "Market Purchase Code",
                ["Plugins.Misc.ReviewReward.MarketCode.Hint"] = "Enter the purchase code provided at the market stall to verify your review and earn a reward coupon.",
                ["Plugins.Misc.ReviewReward.MarketCode.Invalid"] = "The market purchase code entered is invalid or has expired."
            });

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            _migrationManager.ApplyDownMigrations(GetType().Assembly);

            if (_widgetSettings.ActiveWidgetSystemNames.Contains(PluginDescriptor.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Remove(PluginDescriptor.SystemName);
                await _settingService.SaveSettingAsync(_widgetSettings);
            }

            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.ReviewReward");

            await base.UninstallAsync();
        }
    }
}
