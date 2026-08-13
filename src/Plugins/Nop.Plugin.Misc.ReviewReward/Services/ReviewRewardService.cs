using System;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Data;
using Nop.Plugin.Misc.ReviewReward.Domain;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.ReviewReward.Services
{
    public class ReviewRewardService : IReviewRewardService
    {
        private readonly IRepository<MarketPurchaseCode> _marketCodeRepository;
        private readonly IRepository<MarketCodeUsage> _marketCodeUsageRepository;
        private readonly IRepository<ReviewRewardCoupon> _rewardRepository;
        private readonly IRepository<Discount> _discountRepository;
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;
        private readonly IDiscountService _discountService;
        private readonly ISettingService _settingService;
        private readonly IReviewRewardMessageService _reviewRewardMessageService;

        public ReviewRewardService(
            IRepository<MarketPurchaseCode> marketCodeRepository,
            IRepository<MarketCodeUsage> marketCodeUsageRepository,
            IRepository<ReviewRewardCoupon> rewardRepository,
            IRepository<Discount> discountRepository,
            IOrderService orderService,
            IProductService productService,
            IDiscountService discountService,
            ISettingService settingService,
            IReviewRewardMessageService reviewRewardMessageService)
        {
            _marketCodeRepository = marketCodeRepository;
            _marketCodeUsageRepository = marketCodeUsageRepository;
            _rewardRepository = rewardRepository;
            _discountRepository = discountRepository;
            _orderService = orderService;
            _productService = productService;
            _discountService = discountService;
            _settingService = settingService;
            _reviewRewardMessageService = reviewRewardMessageService;
        }

        public async Task<bool> CustomerHasOrderedProductAsync(Customer customer, Product product)
        {
            var orders = await _orderService.SearchOrdersAsync(customerId: customer.Id);
            foreach (var order in orders)
            {
                var items = await _orderService.GetOrderItemsAsync(order.Id, isNotReturnable: false);
                if (items.Any(i => i.ProductId == product.Id))
                    return true;
            }
            return false;
        }

        public async Task<MarketPurchaseCode?> ValidateMarketCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var trimmedCode = code.Trim();
            var match = await _marketCodeRepository.Table
                .FirstOrDefaultAsync(c => c.Code == trimmedCode && c.IsActive);

            if (match == null || match.ExpiryDateUtc < DateTime.UtcNow)
                return null;

            return match;
        }

        public async Task<ReviewRewardCoupon> GrantRewardAsync(Customer customer, ProductReview review,
            MarketPurchaseCode? marketCodeUsed = null)
        {
            var settings = await _settingService.LoadSettingAsync<ReviewRewardSettings>();

            var now = DateTime.UtcNow;
            DateTime? endDate = settings.ExpiryDays > 0 ? now.AddDays(settings.ExpiryDays) : null;
            var prefix = string.IsNullOrWhiteSpace(settings.CouponPrefix) ? "RVW-" : settings.CouponPrefix.Trim();

            var discount = new Discount
            {
                Name = $"Review reward - customer {customer.Id} - review {review.Id}",
                DiscountTypeId = (int)DiscountType.AssignedToOrderTotal,
                UsePercentage = settings.UsePercentage,
                DiscountAmount = settings.UsePercentage ? 0 : settings.RewardAmount,
                DiscountPercentage = settings.UsePercentage ? settings.RewardAmount : 0,
                RequiresCouponCode = true,
                CouponCode = prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                DiscountLimitationId = (int)DiscountLimitationType.NTimesOnly,
                LimitationTimes = 1,
                StartDateUtc = now,
                EndDateUtc = endDate,
                IsActive = true
            };
            await _discountService.InsertDiscountAsync(discount);

            // Attach requirement rule to enforce "only 1 review reward coupon per order"
            var requirement = new DiscountRequirement
            {
                DiscountId = discount.Id,
                DiscountRequirementRuleSystemName = "Misc.ReviewReward"
            };
            await _discountService.InsertDiscountRequirementAsync(requirement);

            var reward = new ReviewRewardCoupon
            {
                CustomerId = customer.Id,
                ProductReviewId = review.Id,
                DiscountId = discount.Id,
                CreatedOnUtc = now
            };
            await _rewardRepository.InsertAsync(reward);

            if (marketCodeUsed != null)
            {
                await _marketCodeUsageRepository.InsertAsync(new MarketCodeUsage
                {
                    MarketPurchaseCodeId = marketCodeUsed.Id,
                    CustomerId = customer.Id,
                    ProductId = review.ProductId,
                    ProductReviewId = review.Id,
                    UsedOnUtc = now
                });
            }

            // Send reward email to customer
            var product = await _productService.GetProductByIdAsync(review.ProductId);
            await _reviewRewardMessageService.SendReviewRewardCouponEmailAsync(customer, product, discount, storeId: review.StoreId);

            return reward;
        }

        public async Task MarkRedeemedManuallyAsync(int reviewRewardCouponId)
        {
            var reward = await _rewardRepository.GetByIdAsync(reviewRewardCouponId);
            if (reward == null || reward.RedeemedOnUtc.HasValue)
                return;

            reward.RedeemedOnUtc = DateTime.UtcNow;
            reward.RedeemedVia = "Market";
            await _rewardRepository.UpdateAsync(reward);

            var discount = await _discountRepository.GetByIdAsync(reward.DiscountId);
            if (discount != null)
            {
                await _discountService.InsertDiscountUsageHistoryAsync(new DiscountUsageHistory
                {
                    DiscountId = discount.Id,
                    CreatedOnUtc = DateTime.UtcNow
                });
            }
        }
    }
}
