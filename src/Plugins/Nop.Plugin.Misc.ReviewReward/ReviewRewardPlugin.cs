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
        private readonly IEmailAccountService _emailAccountService;
        private readonly IWebHelper _webHelper;
        private readonly WidgetSettings _widgetSettings;

        public ReviewRewardPlugin(
            IMigrationManager migrationManager,
            ILocalizationService localizationService,
            ISettingService settingService,
            IMessageTemplateService messageTemplateService,
            IEmailAccountService emailAccountService,
            IWebHelper webHelper,
            WidgetSettings widgetSettings)
        {
            _migrationManager = migrationManager;
            _localizationService = localizationService;
            _settingService = settingService;
            _messageTemplateService = messageTemplateService;
            _emailAccountService = emailAccountService;
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

            // Message template seeding
            var templates = await _messageTemplateService.GetMessageTemplatesByNameAsync(ReviewRewardMessageService.MessageTemplateName);
            if (!templates.Any())
            {
                var emailAccount = (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();
                await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
                {
                    Name = ReviewRewardMessageService.MessageTemplateName,
                    Subject = "%Store.Name%. Thank you for your review! Here is your reward coupon",
                    Body = @"<p><a href=""%Store.URL%"">%Store.Name%</a></p>
<p>Hello %Customer.FullName%,</p>
<p>Thank you for taking the time to review <strong>%ReviewReward.ProductName%</strong>!</p>
<p>As a token of our appreciation, here is your exclusive discount coupon for your next purchase:</p>
<div style=""padding: 15px; background-color: #f8f9fa; border: 1px dashed #28a745; text-align: center; margin: 15px 0;"">
    <span style=""font-size: 18px; font-weight: bold; color: #28a745; letter-spacing: 2px;"">%ReviewReward.CouponCode%</span>
    <p style=""margin-top: 5px; margin-bottom: 0;"">Discount Value: <strong>%ReviewReward.RewardAmount%</strong></p>
</div>
<p>Simply enter this code at checkout on your next order.</p>
<p>Thank you for being a valued customer!</p>",
                    IsActive = true,
                    EmailAccountId = emailAccount?.Id ?? 0
                });
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
