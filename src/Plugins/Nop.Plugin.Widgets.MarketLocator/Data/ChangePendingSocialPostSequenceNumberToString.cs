using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Widgets.MarketLocator.Data;

[NopSchemaMigration("2026-05-08 14:00:00", "Widgets.MarketLocator change pending social post sequence number to string raw SQL")]
public class ChangePendingSocialPostSequenceNumberToString : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'PendingSocialPostSequenceNumber')
            BEGIN
                EXEC sp_rename 'MarketLocation.PendingSocialPostSequenceNumber', 'PendingSocialPostSequenceNumbers', 'COLUMN';
                ALTER TABLE [MarketLocation] ALTER COLUMN [PendingSocialPostSequenceNumbers] NVARCHAR(MAX) NULL;
            END
            ELSE IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'PendingSocialPostSequenceNumbers')
            BEGIN
                ALTER TABLE [MarketLocation] ADD [PendingSocialPostSequenceNumbers] NVARCHAR(MAX) NULL;
            END");
    }

    public override void Down()
    {
    }
}
