using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.ReviewReward.Domain
{
    public class ReviewRewardSettings : ISettings
    {
        /// <summary>
        /// Fixed discount amount (e.g. £5.00) or percentage value (e.g. 10%).
        /// </summary>
        public decimal RewardAmount { get; set; } = 5.00m;

        /// <summary>
        /// True if RewardAmount is a percentage (e.g. 10%), false if fixed currency amount.
        /// </summary>
        public bool UsePercentage { get; set; } = false;

        /// <summary>
        /// Prefix for generated coupon codes (e.g., "RVW-").
        /// </summary>
        public string CouponPrefix { get; set; } = "RVW-";

        /// <summary>
        /// Days until generated reward coupon expires (0 = no expiry).
        /// </summary>
        public int ExpiryDays { get; set; } = 30;
    }
}
