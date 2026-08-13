using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Misc.ReviewReward.Domain;

namespace Nop.Plugin.Misc.ReviewReward.Services
{
    public interface IReviewRewardService
    {
        /// <summary>
        /// True if the customer has a completed order containing this product.
        /// Drives which verification path the review form offers.
        /// </summary>
        Task<bool> CustomerHasOrderedProductAsync(Customer customer, Product product);

        /// <summary>
        /// Validates a market code: exists, active, not expired. Returns null if invalid.
        /// </summary>
        Task<MarketPurchaseCode?> ValidateMarketCodeAsync(string code);

        /// <summary>
        /// Called after a review is saved (online-verified or market-code path).
        /// Creates the per-customer Discount + ReviewRewardCoupon, logs market code
        /// usage if applicable, and sends the reward email. Reviews from registered
        /// customers are auto-approved, so this runs synchronously on submission.
        /// </summary>
        Task<ReviewRewardCoupon> GrantRewardAsync(Customer customer, ProductReview review, MarketPurchaseCode? marketCodeUsed = null);

        /// <summary>
        /// Admin action: mark a reward as redeemed when handed over at a market,
        /// bypassing the normal online checkout redemption path.
        /// </summary>
        Task MarkRedeemedManuallyAsync(int reviewRewardCouponId);
    }
}
