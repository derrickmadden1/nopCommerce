using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Widgets.MarketLocator.Data;

[NopSchemaMigration("2026-04-15 14:00:00", "Widgets.MarketLocator add pending social post sequence number raw SQL")]
public class AddPendingSocialPostSequenceNumber : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'PendingSocialPostSequenceNumber')
            BEGIN
                ALTER TABLE [MarketLocation] ADD [PendingSocialPostSequenceNumber] bigint NULL;
            END");
    }

    public override void Down()
    {
    }
}
