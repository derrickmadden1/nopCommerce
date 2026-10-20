using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Messages;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Messages;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.ReviewReward.Services
{
    public class ReviewRewardMessageService : IReviewRewardMessageService
    {
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly IMessageTemplateService _messageTemplateService;
        private readonly IEmailAccountService _emailAccountService;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly IProductService _productService;
        private readonly IRepository<DiscountProductMapping> _discountProductMappingRepository;

        public const string MessageTemplateName = "ReviewReward.CouponEarned";

        public const string DefaultSubject = "%Store.Name%. Thank you for your review! Here is your reward coupon";

        public const string DefaultBody = @"<p><a href=""%Store.URL%"">%Store.Name%</a></p>
<p>Hello %Customer.FullName%,</p>
<p>Thank you for taking the time to review <strong>%ReviewReward.ProductName%</strong>!</p>
<p>As a token of our appreciation, here is your exclusive discount reward coupon:</p>

<div style=""padding: 20px; background-color: #f8f9fa; border: 2px dashed #28a745; border-radius: 8px; text-align: center; margin: 20px 0;"">
    <div style=""font-size: 13px; text-transform: uppercase; color: #6c757d; letter-spacing: 1px; margin-bottom: 5px;"">Your Coupon Code</div>
    <div style=""font-size: 24px; font-weight: bold; color: #28a745; letter-spacing: 3px; font-family: monospace;"">%ReviewReward.CouponCode%</div>
    <div style=""font-size: 16px; margin-top: 8px; color: #343a40;"">Reward Value: <strong>%ReviewReward.RewardAmount%</strong></div>
</div>

<h4 style=""color: #343a40; margin-bottom: 10px;"">How to Redeem Your Reward:</h4>
<ul style=""color: #495057; line-height: 1.6; padding-left: 20px;"">
    <li><strong>Online:</strong> Enter your coupon code during checkout in the discount code box (if offering a free product, it is automatically added to your basket when applied).</li>
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

        public ReviewRewardMessageService(
            IWorkflowMessageService workflowMessageService,
            IMessageTokenProvider messageTokenProvider,
            IMessageTemplateService messageTemplateService,
            IEmailAccountService emailAccountService,
            IStoreContext storeContext,
            IStoreService storeService,
            IProductService productService,
            IRepository<DiscountProductMapping> discountProductMappingRepository)
        {
            _workflowMessageService = workflowMessageService;
            _messageTokenProvider = messageTokenProvider;
            _messageTemplateService = messageTemplateService;
            _emailAccountService = emailAccountService;
            _storeContext = storeContext;
            _storeService = storeService;
            _productService = productService;
            _discountProductMappingRepository = discountProductMappingRepository;
        }

        public async Task EnsureMessageTemplateExistsAsync(bool overwriteExisting = false)
        {
            var templates = await _messageTemplateService.GetMessageTemplatesByNameAsync(MessageTemplateName);
            if (!templates.Any())
            {
                var emailAccount = (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();
                await _messageTemplateService.InsertMessageTemplateAsync(new MessageTemplate
                {
                    Name = MessageTemplateName,
                    Subject = DefaultSubject,
                    Body = DefaultBody,
                    IsActive = true,
                    EmailAccountId = emailAccount?.Id ?? 0
                });
            }
            else if (overwriteExisting)
            {
                foreach (var template in templates)
                {
                    template.Subject = DefaultSubject;
                    template.Body = DefaultBody;
                    await _messageTemplateService.UpdateMessageTemplateAsync(template);
                }
            }
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

            await EnsureMessageTemplateExistsAsync();

            var messageTemplates = await _workflowMessageService.GetActiveMessageTemplatesAsync(MessageTemplateName, store.Id);
            if (!messageTemplates.Any())
                return;

            string rewardAmountText;
            if (discount.DiscountType == DiscountType.AssignedToSkus)
            {
                var mapping = await _discountProductMappingRepository.Table
                    .FirstOrDefaultAsync(m => m.DiscountId == discount.Id);
                var freeProduct = mapping != null ? await _productService.GetProductByIdAsync(mapping.EntityId) : null;
                rewardAmountText = freeProduct != null
                    ? $"Free Product: {freeProduct.Name} (100% off)"
                    : "Free Product (100% off)";
            }
            else
            {
                rewardAmountText = discount.UsePercentage
                    ? $"{discount.DiscountPercentage:G29}% off"
                    : $"{discount.DiscountAmount:C2} off";
            }

            string expiryDateText = discount.EndDateUtc.HasValue
                ? discount.EndDateUtc.Value.ToString("dd MMMM yyyy")
                : "No Expiration Date";

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
                tokens.Add(new Token("ReviewReward.ExpiryDate", expiryDateText));

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
