using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.ReviewReward.Domain;

namespace Nop.Plugin.Misc.ReviewReward.Data
{
    public class MarketPurchaseCodeBuilder : NopEntityBuilder<MarketPurchaseCode>
    {
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(MarketPurchaseCode.Code)).AsString(100).NotNullable()
                .WithColumn(nameof(MarketPurchaseCode.Description)).AsString(400).Nullable();
        }
    }

    public class MarketCodeUsageBuilder : NopEntityBuilder<MarketCodeUsage>
    {
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(MarketCodeUsage.MarketPurchaseCodeId)).AsInt32().ForeignKey<MarketPurchaseCode>()
                .WithColumn(nameof(MarketCodeUsage.CustomerId)).AsInt32()
                .WithColumn(nameof(MarketCodeUsage.ProductId)).AsInt32()
                .WithColumn(nameof(MarketCodeUsage.ProductReviewId)).AsInt32();
        }
    }

    public class ReviewRewardCouponBuilder : NopEntityBuilder<ReviewRewardCoupon>
    {
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(ReviewRewardCoupon.CustomerId)).AsInt32()
                .WithColumn(nameof(ReviewRewardCoupon.ProductReviewId)).AsInt32()
                .WithColumn(nameof(ReviewRewardCoupon.DiscountId)).AsInt32()
                .WithColumn(nameof(ReviewRewardCoupon.RedeemedVia)).AsString(50).Nullable();
        }
    }
}
