using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Data;
using Nop.Plugin.Misc.ReviewReward.Domain;
using Nop.Services.Discounts;
using Nop.Services.Messages;
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
        private readonly IDiscountService _discountService;
        private readonly IWorkflowMessageService _workflowMessageService;

        public ReviewRewardService(
            IRepository<MarketPurchaseCode> marketCodeRepository,
            IRepository<MarketCodeUsage> marketCodeUsageRepository,
            IRepository<ReviewRewardCoupon> rewardRepository,
            IRepository<Discount> discountRepository,
            IOrderService orderService,
            IDiscountService discountService,
            IWorkflowMessageService workflowMessageService)
        {
            _marketCodeRepository = marketCodeRepository;
            _marketCodeUsageRepository = marketCodeUsageRepository;
            _rewardRepository = rewardRepository;
            _discountRepository = discountRepository;
            _orderService = orderService;
            _discountService = discountService;
            _workflowMessageService = workflowMessageService;
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
            var discount = new Discount
            {
                Name = $"Review reward - customer {customer.Id} - review {review.Id}",
                DiscountTypeId = (int)DiscountType.AssignedToOrderTotal,
                UsePercentage = false,
                DiscountAmount = 0,
                RequiresCouponCode = true,
                CouponCode = GenerateCouponCode(),
                DiscountLimitationId = (int)DiscountLimitationType.NTimesOnly,
                LimitationTimes = 1,
                IsActive = true
            };
            await _discountService.InsertDiscountAsync(discount);

            var reward = new ReviewRewardCoupon
            {
                CustomerId = customer.Id,
                ProductReviewId = review.Id,
                DiscountId = discount.Id,
                CreatedOnUtc = DateTime.UtcNow
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
                    UsedOnUtc = DateTime.UtcNow
                });
            }

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

        private static string GenerateCouponCode()
        {
            return "RVW-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        }
    }
}
