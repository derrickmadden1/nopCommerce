using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.ReviewReward.Domain;

namespace Nop.Plugin.Misc.ReviewReward.Migrations
{
    [NopMigration("2026-08-13 00:00:00", "Misc.ReviewReward base schema", MigrationProcessType.Installation)]
    public class SchemaMigration : Migration
    {
        public override void Up()
        {
            this.CreateTableIfNotExists<MarketPurchaseCode>();
            this.CreateTableIfNotExists<MarketCodeUsage>();
            this.CreateTableIfNotExists<ReviewRewardCoupon>();
        }

        public override void Down()
        {
            this.DeleteTableIfExists<ReviewRewardCoupon>();
            this.DeleteTableIfExists<MarketCodeUsage>();
            this.DeleteTableIfExists<MarketPurchaseCode>();
        }
    }
}
