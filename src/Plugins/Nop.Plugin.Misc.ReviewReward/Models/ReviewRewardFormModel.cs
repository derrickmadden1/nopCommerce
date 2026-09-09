using Nop.Web.Framework.Models;

namespace Nop.Plugin.Misc.ReviewReward.Models
{
    public record ReviewRewardFormModel : BaseNopModel
    {
        public int ProductId { get; set; }

        /// <summary>
        /// True if the customer has an online order history for this product.
        /// </summary>
        public bool HasOrderedProduct { get; set; }

        /// <summary>
        /// Code entered by market stall customers.
        /// </summary>
        public string? MarketPurchaseCode { get; set; }

        /// <summary>
        /// Error message if market code validation failed on POST.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
