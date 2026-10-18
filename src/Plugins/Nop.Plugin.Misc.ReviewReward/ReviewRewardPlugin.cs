using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Messages;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.ReviewReward.Components;
using Nop.Plugin.Misc.ReviewReward.Domain;
using Nop.Plugin.Misc.ReviewReward.Services;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Misc.ReviewReward
{
    public class ReviewRewardPlugin : BasePlugin, IMiscPlugin, IWidgetPlugin
    {
        private readonly IMigrationManager _migrationManager;
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IMessageTemplateService _messageTemplateService;
        private readonly IReviewRewardMessageService _reviewRewardMessageService;
        private readonly IWebHelper _webHelper;
        private readonly WidgetSettings _widgetSettings;

        public ReviewRewardPlugin(
            IMigrationManager migrationManager,
            ILocalizationService localizationService,
            ISettingService settingService,
            IMessageTemplateService messageTemplateService,
            IReviewRewardMessageService reviewRewardMessageService,
            IWebHelper webHelper,
            WidgetSettings widgetSettings)
        {
            _migrationManager = migrationManager;
            _localizationService = localizationService;
            _settingService = settingService;
            _messageTemplateService = messageTemplateService;
            _reviewRewardMessageService = reviewRewardMessageService;
            _webHelper = webHelper;
            _widgetSettings = widgetSettings;
        }

        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>(new List<string>
            {
                PublicWidgetZones.ProductReviewsPageInsideForm,
                PublicWidgetZones.ProductReviewsPageBottom
            });
        }

        public Type GetWidgetViewComponent(string widgetZone)
        {
            return typeof(ReviewRewardReviewFormViewComponent);
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/ReviewRewardAdmin/Configure";
        }

        public bool HideInWidgetList => false;

        public override async Task InstallAsync()
        {
            _migrationManager.ApplyUpMigrations(GetType().Assembly);

            await _settingService.SaveSettingAsync(new ReviewRewardSettings
            {
                RewardAmount = 5.00m,
                UsePercentage = false,
                CouponPrefix = "RVW-",
                ExpiryDays = 30
            });

            if (!_widgetSettings.ActiveWidgetSystemNames.Contains(PluginDescriptor.SystemName))
            {
                _widgetSettings.ActiveWidgetSystemNames.Add(PluginDescriptor.SystemName);
                await _settingService.SaveSettingAsync(_widgetSettings);
            }

            await _reviewRewardMessageService.EnsureMessageTemplateExistsAsync(overwriteExisting: true);

            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Misc.ReviewReward.MarketCode"] = "Market Purchase Code",
                ["Plugins.Misc.ReviewReward.MarketCode.Hint"] = "Enter the purchase code provided at the market stall to verify your review and earn a reward coupon.",
                ["Plugins.Misc.ReviewReward.MarketCode.Invalid"] = "The market purchase code entered is invalid or has expired."
            });

            await base.InstallAsync();
        }

        public override async Task UpdateAsync(string currentVersion, string targetVersion)
        {
            await _reviewRewardMessageService.EnsureMessageTemplateExistsAsync(overwriteExisting: false);

            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Misc.ReviewReward.MarketCode"] = "Market Purchase Code",
                ["Plugins.Misc.ReviewReward.MarketCode.Hint"] = "Enter the purchase code provided at the market stall to verify your review and earn a reward coupon.",
                ["Plugins.Misc.ReviewReward.MarketCode.Invalid"] = "The market purchase code entered is invalid or has expired."
            });

            await base.UpdateAsync(currentVersion, targetVersion);
        }

        public override async Task UninstallAsync()
        {
            _migrationManager.ApplyDownMigrations(GetType().Assembly);

            await _settingService.DeleteSettingAsync<ReviewRewardSettings>();

            var templates = await _messageTemplateService.GetMessageTemplatesByNameAsync(ReviewRewardMessageService.MessageTemplateName);
            foreach (var template in templates)
            {
                await _messageTemplateService.DeleteMessageTemplateAsync(template);
            }

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
