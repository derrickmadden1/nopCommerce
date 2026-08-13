using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Messages;
using Nop.Plugin.Misc.ReviewReward.Domain;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.ReviewReward.Services
{
    public class ReviewRewardMessageService : IReviewRewardMessageService
    {
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly ILanguageService _languageService;
        private readonly ISettingService _settingService;

        public const string MessageTemplateName = "ReviewReward.CouponEarned";

        public ReviewRewardMessageService(
            IWorkflowMessageService workflowMessageService,
            IMessageTokenProvider messageTokenProvider,
            IStoreContext storeContext,
            IStoreService storeService,
            ILanguageService languageService,
            ISettingService settingService)
        {
            _workflowMessageService = workflowMessageService;
            _messageTokenProvider = messageTokenProvider;
            _storeContext = storeContext;
            _storeService = storeService;
            _languageService = languageService;
            _settingService = settingService;
        }

        public async Task SendReviewRewardCouponEmailAsync(Customer customer, Product product, Discount discount, int languageId = 0, int storeId = 0)
        {
            if (customer == null || string.IsNullOrWhiteSpace(customer.Email))
                return;

            var store = storeId > 0
                ? await _storeService.GetStoreByIdAsync(storeId)
                : await _storeContext.GetCurrentStoreAsync();

            if (store == null)
                return;

            if (languageId == 0)
                languageId = store.DefaultLanguageId;

            var messageTemplates = await _workflowMessageService.GetActiveMessageTemplatesAsync(MessageTemplateName, store.Id);
            if (!messageTemplates.Any())
                return;

            var settings = await _settingService.LoadSettingAsync<ReviewRewardSettings>();

            string rewardAmountText = settings.UsePercentage
                ? $"{settings.RewardAmount:G29}%"
                : $"{discount.DiscountAmount:C2}";

            var commonTokens = new List<Token>();
            await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, customer);

            foreach (var messageTemplate in messageTemplates)
            {
                var emailAccount = await _workflowMessageService.GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);
                var tokens = new List<Token>(commonTokens);

                await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

                // Add ReviewReward specific tokens
                tokens.Add(new Token("ReviewReward.CouponCode", discount.CouponCode));
                tokens.Add(new Token("ReviewReward.RewardAmount", rewardAmountText));
                tokens.Add(new Token("ReviewReward.ProductName", product?.Name ?? string.Empty));

                await _workflowMessageService.SendNotificationAsync(
                    messageTemplate,
                    emailAccount,
                    languageId,
                    tokens,
                    customer.Email,
                    customer.Username ?? customer.Email);
            }
        }
    }
}
