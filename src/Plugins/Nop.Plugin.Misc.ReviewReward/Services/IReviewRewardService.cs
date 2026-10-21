using System.Threading.Tasks;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
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
        /// usage if applicable, adds the free product + coupon to the customer's basket
        /// if configured and no other review reward is currently applied, and sends the reward email.
        /// </summary>
        Task<ReviewRewardCoupon> GrantRewardAsync(Customer customer, ProductReview review, MarketPurchaseCode? marketCodeUsed = null);

        /// <summary>
        /// Admin action: mark a reward as redeemed when handed over at a market,
        /// bypassing the normal online checkout redemption path.
        /// </summary>
        Task MarkRedeemedManuallyAsync(int reviewRewardCouponId);

        /// <summary>
        /// Returns the free product associated with a Review Reward discount if it was issued as a Free Product reward; otherwise null.
        /// </summary>
        Task<Product?> GetFreeProductForDiscountAsync(int discountId);

        /// <summary>
        /// True if the customer currently has an active, unredeemed Review Reward coupon code applied in their basket.
        /// </summary>
        Task<bool> HasActiveReviewRewardCouponAppliedAsync(Customer customer, int? excludeDiscountId = null);

        /// <summary>
        /// When a customer manually applies a Free Product Review Reward coupon code in the cart,
        /// adds 1 unit of the associated free product to the cart. Returns true if the cart was modified.
        /// </summary>
        Task<bool> HandleCouponAppliedInCartAsync(Customer customer, string couponCode, int storeId);

        /// <summary>
        /// When a customer removes a Free Product Review Reward coupon code from the cart,
        /// removes 1 unit of the associated free product from the cart. Returns true if the cart was modified.
        /// </summary>
        Task<bool> HandleCouponRemovedFromCartAsync(Customer customer, int discountId, int storeId);

        /// <summary>
        /// When a customer removes the free product item itself from the cart, removes any applied
        /// Free Product Review Reward coupon code for that product so another code can be used.
        /// </summary>
        Task HandleCartItemDeletedAsync(ShoppingCartItem deletedItem);

        /// <summary>
        /// Marks any Review Reward coupon used on a newly placed order as redeemed online.
        /// </summary>
        Task HandleOrderPlacedAsync(Order order);
    }
}
