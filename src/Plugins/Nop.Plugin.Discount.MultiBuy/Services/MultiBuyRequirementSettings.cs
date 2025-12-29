using System.Collections.Generic;

namespace Nop.Plugin.DiscountRules.MultiBuy.Services
{
    /// <summary>
    /// Per-discount-requirement settings for the multi-buy rule.
    /// Stored via ISettingService using a key that includes the discountRequirementId.
    /// </summary>
    public class MultiBuyRequirementSettings
    {
        /// <summary>
        /// Number of items in one bundle (e.g. 2 for "2 for £20").
        /// </summary>
        public int BundleSize { get; set; } = 2;

        /// <summary>
        /// Price for one bundle (e.g. 20.00 for "2 for £20").
        /// </summary>
        public decimal BundlePrice { get; set; }

        /// <summary>
        /// Whether different products from the eligible list can be mixed within a bundle.
        /// </summary>
        public bool ApplyAcrossMixedProducts { get; set; } = true;

        /// <summary>
        /// List of product identifiers that can participate in the bundle.
        /// </summary>
        public List<int> EligibleProductIds { get; set; } = new();
    }
}


