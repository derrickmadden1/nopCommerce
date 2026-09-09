using Nop.Core;

namespace Nop.Plugin.Misc.ReviewReward.Domain
{
    /// <summary>
    /// A code issued for a market (not per-customer, not per-product). Any registered
    /// customer can use it to unlock reviews for any number of products, as long as it
    /// hasn't expired. No "IsRedeemed" flag - this is intentionally reusable.
    /// </summary>
    public class MarketPurchaseCode : BaseEntity
    {
        /// <summary>
        /// The code text customers enter, e.g. "ROSECOTTAGE-AUG26".
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Optional free-text description, e.g. "Newark Market - 9 Aug 2026", for admin reference.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Codes stop validating after this date. Defaulting to a period
        /// after the market so customers have time to submit reviews.
        /// </summary>
        public DateTime ExpiryDateUtc { get; set; }

        /// <summary>
        /// Manual on/off switch independent of expiry, in case a code needs to be
        /// disabled early (e.g. leaked/shared beyond the intended market).
        /// </summary>
        public bool IsActive { get; set; }

        public DateTime CreatedOnUtc { get; set; }
    }
}
