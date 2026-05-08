using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Widgets.MarketLocator.Data;

[NopSchemaMigration("2026-04-15 14:01:00", "Widgets.MarketLocator add Description field raw SQL")]
public class AddDescriptionToMarketLocation : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'Description')
            BEGIN
                ALTER TABLE [MarketLocation] ADD [Description] nvarchar(MAX) NULL;
            END");
    }

    public override void Down()
    {
    }
}
