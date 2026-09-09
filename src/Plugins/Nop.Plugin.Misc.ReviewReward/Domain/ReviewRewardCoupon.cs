using Nop.Core;

namespace Nop.Plugin.Misc.ReviewReward.Domain
{
    /// <summary>
    /// One row per earned reward. Wraps a real nopCommerce Discount/coupon code
    /// (each reward is its own Discount record, since core Discount only supports
    /// a single CouponCode per record - there's no native concept of many codes
    /// sharing one discount definition).
    /// </summary>
    public class ReviewRewardCoupon : BaseEntity
    {
        public int CustomerId { get; set; }

        public int ProductReviewId { get; set; }

        /// <summary>
        /// FK to the generated Nop.Core.Domain.Discounts.Discount record. That record
        /// holds the actual CouponCode, discount amount/percentage, and usage limit.
        /// </summary>
        public int DiscountId { get; set; }

        public DateTime CreatedOnUtc { get; set; }

        /// <summary>
        /// Set when redeemed. Online redemption sets this from the order-processing
        /// pipeline; market redemption sets this via the admin "mark as used" action.
        /// </summary>
        public DateTime? RedeemedOnUtc { get; set; }

        /// <summary>
        /// "Online" or "Market" or "Admin" - where the mark-as-used action came from.
        /// Useful for reconciling later; not used for any validation logic.
        /// </summary>
        public string? RedeemedVia { get; set; }
    }
}
