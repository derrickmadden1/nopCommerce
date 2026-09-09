using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Domain;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Migrations
{
    // IMPORTANT: update this timestamp to match when you actually run the
    // install — do not leave it backdated to when this was drafted.
    [NopMigration("2026-08-09 00:00:00", "CheckoutAttempt table", MigrationProcessType.Update)]
    public class SchemaMigration : Migration
    {
        public override void Up()
        {
            this.CreateTableIfNotExists<CheckoutAttempt>();

            // Indexes matter here — RecordStepAsync and MarkCompletedAsync both
            // filter on these columns on every checkout step, so unindexed this
            // will get slow once the table has any real volume.
            Create.Index("IX_CheckoutAttempt_CustomerGuid_Store_Open")
                .OnTable(nameof(CheckoutAttempt))
                .OnColumn(nameof(CheckoutAttempt.CustomerGuid)).Ascending()
                .OnColumn(nameof(CheckoutAttempt.StoreId)).Ascending()
                .OnColumn(nameof(CheckoutAttempt.OrderId)).Ascending()
                .OnColumn(nameof(CheckoutAttempt.IsAbandoned)).Ascending();

            Create.Index("IX_CheckoutAttempt_CustomerId_Open")
                .OnTable(nameof(CheckoutAttempt))
                .OnColumn(nameof(CheckoutAttempt.CustomerId)).Ascending()
                .OnColumn(nameof(CheckoutAttempt.OrderId)).Ascending()
                .OnColumn(nameof(CheckoutAttempt.IsAbandoned)).Ascending();

            // Supports FlagAbandonedAsync's scan for stale open attempts
            Create.Index("IX_CheckoutAttempt_LastActivity_Open")
                .OnTable(nameof(CheckoutAttempt))
                .OnColumn(nameof(CheckoutAttempt.LastActivityUtc)).Ascending()
                .OnColumn(nameof(CheckoutAttempt.OrderId)).Ascending()
                .OnColumn(nameof(CheckoutAttempt.IsAbandoned)).Ascending();
        }

        public override void Down()
        {
            // Deliberately not dropping the table here — see README.
            // If you do want uninstall to remove data, uncomment:
            // Delete.Table(nameof(CheckoutAttempt));
        }
    }
}

