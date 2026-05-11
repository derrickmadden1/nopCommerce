using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Widgets.MarketLocator.Data;

[NopSchemaMigration("2026-05-09 18:00:00", "Widgets.MarketLocator add LastModifiedUtc field raw SQL")]
public class AddLastModifiedUtcToMarketLocation : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'LastModifiedUtc')
            BEGIN
                ALTER TABLE [MarketLocation] ADD [LastModifiedUtc] datetime2 NOT NULL DEFAULT GETUTCDATE();
            END");
    }

    public override void Down()
    {
    }
}
