using FluentMigrator;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.MarketLocator.Domain;

namespace Nop.Plugin.Widgets.MarketLocator.Data;

[NopSchemaMigration("2026-07-17 10:00:00", "Widgets.MarketLocator add PictureId raw SQL")]
public class AddPictureIdToMarketLocation : Migration
{
    public override void Up()
    {
        // FluentMigrator's typical SQL execution
        Execute.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'PictureId')
            BEGIN
                ALTER TABLE [MarketLocation] ADD [PictureId] int NOT NULL DEFAULT 0;
            END
        ");
    }

    public override void Down()
    {
        Execute.Sql(@"
            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'PictureId')
            BEGIN
                ALTER TABLE [MarketLocation] DROP COLUMN [PictureId];
            END
        ");
    }
}
