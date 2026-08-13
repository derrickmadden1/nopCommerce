using System;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Misc.ReviewReward.Models
{
    public record ReviewRewardConfigureModel : BaseNopModel
    {
        public decimal RewardAmount { get; set; }
        public bool UsePercentage { get; set; }
        public string? CouponPrefix { get; set; }
        public int ExpiryDays { get; set; }
    }

    public record MarketPurchaseCodeSearchModel : BaseSearchModel
    {
    }

    public record MarketPurchaseCodeModel : BaseNopEntityModel
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiryDateUtc { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedOnUtc { get; set; }
    }

    public record MarketPurchaseCodeListModel : BasePagedListModel<MarketPurchaseCodeModel>
    {
    }

    public record ReviewRewardCouponSearchModel : BaseSearchModel
    {
    }

    public record ReviewRewardCouponModel : BaseNopEntityModel
    {
        public int CustomerId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public int ProductReviewId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CouponCode { get; set; } = string.Empty;
        public string DiscountAmountText { get; set; } = string.Empty;
        public DateTime CreatedOnUtc { get; set; }
        public DateTime? RedeemedOnUtc { get; set; }
        public string? RedeemedVia { get; set; }
        public bool IsRedeemed { get; set; }
    }

    public record ReviewRewardCouponListModel : BasePagedListModel<ReviewRewardCouponModel>
    {
    }
}
