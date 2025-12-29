using System.Collections.Generic;

namespace Nop.Plugin.DiscountRules.MultiBuy.Models
{
    /// <summary>
    /// Full configuration model for a single multi-buy discount requirement.
    /// </summary>
    public record ConfigureModel : RequirementModel
    {
        /// <summary>
        /// Number of items in one bundle (e.g. 2 for \"2 for £20\").
        /// </summary>
        public int BundleSize { get; set; }

        /// <summary>
        /// Price for one bundle.
        /// </summary>
        public decimal BundlePrice { get; set; }

        /// <summary>
        /// Whether different products from the eligible list can be mixed within a bundle.
        /// </summary>
        public bool ApplyAcrossMixedProducts { get; set; }

        /// <summary>
        /// Comma-separated list of eligible product IDs (backed by RequirementModel.ProductIds for storage).
        /// </summary>
        public List<int> EligibleProductIds { get; set; } = new();

        public List<ProductDisplayModel> EligibleProducts { get; set; } = new();
    }
}