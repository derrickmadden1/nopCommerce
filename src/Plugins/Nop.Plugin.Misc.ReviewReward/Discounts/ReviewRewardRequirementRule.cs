using System;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core.Domain.Discounts;
using Nop.Data;
using Nop.Plugin.Misc.ReviewReward.Domain;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.ReviewReward.Discounts
{
    /// <summary>
    /// Blocks a second review-reward coupon being applied to the same order.
    /// Ordinary (non-review-reward) promo codes are untouched, so they can still
    /// stack alongside a single review-reward code per requirement.
    /// </summary>
    public class ReviewRewardRequirementRule : BasePlugin, IDiscountRequirementRule
    {
        private readonly IRepository<ReviewRewardCoupon> _rewardRepository;
        private readonly IRepository<Discount> _discountRepository;
        private readonly IDiscountService _discountService;
        private readonly ICustomerService _customerService;

        public ReviewRewardRequirementRule(
            IRepository<ReviewRewardCoupon> rewardRepository,
            IRepository<Discount> discountRepository,
            IDiscountService discountService,
            ICustomerService customerService)
        {
            _rewardRepository = rewardRepository;
            _discountRepository = discountRepository;
            _discountService = discountService;
            _customerService = customerService;
        }

        public async Task<DiscountRequirementValidationResult> CheckRequirementAsync(DiscountRequirementValidationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var requirement = await _discountService.GetDiscountRequirementByIdAsync(request.DiscountRequirementId);
            if (requirement == null)
                return new DiscountRequirementValidationResult { IsValid = true };

            var isReviewReward = _rewardRepository.Table.Any(r => r.DiscountId == requirement.DiscountId);
            if (!isReviewReward)
                return new DiscountRequirementValidationResult { IsValid = true };

            if (request.Customer == null)
                return new DiscountRequirementValidationResult { IsValid = true };

            // Inspect other applied coupon codes on the customer's cart
            var appliedCouponCodes = await _customerService.ParseAppliedDiscountCouponCodesAsync(request.Customer);
            if (appliedCouponCodes == null || appliedCouponCodes.Length == 0)
                return new DiscountRequirementValidationResult { IsValid = true };

            // Find all discounts associated with applied coupon codes
            var currentDiscountId = requirement.DiscountId;
            var otherAppliedDiscountIds = _discountRepository.Table
                .Where(d => appliedCouponCodes.Contains(d.CouponCode) && d.Id != currentDiscountId)
                .Select(d => d.Id)
                .ToList();

            if (otherAppliedDiscountIds.Count == 0)
                return new DiscountRequirementValidationResult { IsValid = true };

            // Check if any of those other applied discounts is ALSO a review reward coupon
            var hasOtherReviewReward = _rewardRepository.Table.Any(r => otherAppliedDiscountIds.Contains(r.DiscountId));
            if (hasOtherReviewReward)
            {
                return new DiscountRequirementValidationResult
                {
                    IsValid = false,
                    UserError = "Only one review reward coupon code can be applied per order."
                };
            }

            return new DiscountRequirementValidationResult { IsValid = true };
        }

        public string GetConfigurationUrl(int discountId, int? discountRequirementId)
        {
            return string.Empty;
        }
    }
}
