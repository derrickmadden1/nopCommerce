using Nop.Core;

namespace Nop.Plugin.Misc.ReviewReward.Domain
{
    /// <summary>
    /// A log row created every time a market code successfully unlocks a review.
    /// Purely for admin visibility/reporting - does not restrict reuse of the code.
    /// </summary>
    public class MarketCodeUsage : BaseEntity
    {
        public int MarketPurchaseCodeId { get; set; }

        public int CustomerId { get; set; }

        public int ProductId { get; set; }

        /// <summary>
        /// The review this usage led to, once submitted.
        /// </summary>
        public int ProductReviewId { get; set; }

        public DateTime UsedOnUtc { get; set; }
    }
}
