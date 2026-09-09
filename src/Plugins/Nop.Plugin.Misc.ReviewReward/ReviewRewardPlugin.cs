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
            var emailAccount = (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

            const string templateSubject = "%Store.Name%. Thank you for your review! Here is your reward coupon";
            const string templateBody = @"<p><a href=""%Store.URL%"">%Store.Name%</a></p>
<p>Hello %Customer.FullName%,</p>
<p>Thank you for taking the time to review <strong>%ReviewReward.ProductName%</strong>!</p>
<p>As a token of our appreciation, here is your exclusive discount reward coupon:</p>

<div style=""padding: 20px; background-color: #f8f9fa; border: 2px dashed #28a745; border-radius: 8px; text-align: center; margin: 20px 0;"">
    <div style=""font-size: 13px; text-transform: uppercase; color: #6c757d; letter-spacing: 1px; margin-bottom: 5px;"">Your Coupon Code</div>
    <div style=""font-size: 24px; font-weight: bold; color: #28a745; letter-spacing: 3px; font-family: monospace;"">%ReviewReward.CouponCode%</div>
    <div style=""font-size: 16px; margin-top: 8px; color: #343a40;"">Discount Value: <strong>%ReviewReward.RewardAmount%</strong></div>
</div>

<h4 style=""color: #343a40; margin-bottom: 10px;"">How to Redeem Your Discount:</h4>
<ul style=""color: #495057; line-height: 1.6; padding-left: 20px;"">
    <li><strong>Online:</strong> Enter your coupon code during checkout in the discount code box.</li>
    <li><strong>In-Person at Market Stall:</strong> Present this email or coupon code to stall staff at any of our market locations.</li>
</ul>

<h4 style=""color: #343a40; margin-bottom: 10px;"">Terms & Conditions:</h4>
<ul style=""color: #6c757d; font-size: 0.9em; line-height: 1.6; padding-left: 20px;"">
    <li><strong>Single Use Only:</strong> This code can only be redeemed once (either online OR at a market stall).</li>
    <li><strong>Expiry Date:</strong> %ReviewReward.ExpiryDate%</li>
    <li><strong>Cart Stacking:</strong> Can be combined with standard promo codes, but only one review reward coupon is allowed per order.</li>
</ul>

<p>Thank you for being a valued customer!</p>
<p>Best regards,<br/><strong>%Store.Name% Team</strong></p>";

            if (!templates.Any())
            {
                await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
                {
                    Name = ReviewRewardMessageService.MessageTemplateName,
                    Subject = templateSubject,
                    Body = templateBody,
                    IsActive = true,
                    EmailAccountId = emailAccount?.Id ?? 0
                });
            }
            else
            {
                foreach (var template in templates)
                {
                    template.Subject = templateSubject;
                    template.Body = templateBody;
                    await _messageTemplateService.UpdateMessageTemplateAsync(template);
                }
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
