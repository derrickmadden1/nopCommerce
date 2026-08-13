using System;
using System.Linq;
using System.Threading.Tasks;
using Nop.Data;
using Nop.Plugin.Misc.ReviewReward.Domain;
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
        private readonly IDiscountService _discountService;

        public ReviewRewardRequirementRule(
            IRepository<ReviewRewardCoupon> rewardRepository,
            IDiscountService discountService)
        {
            _rewardRepository = rewardRepository;
            _discountService = discountService;
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

            return new DiscountRequirementValidationResult { IsValid = true };
        }

        public string GetConfigurationUrl(int discountId, int? discountRequirementId)
        {
            return string.Empty;
        }
    }
}
