using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Widgets.MarketLocator.Data;

[NopSchemaMigration("2026-09-22 09:00:00", "Widgets.MarketLocator add social publish targets")]
public class AddSocialPublishTargetsToMarketLocation : Migration
{
    public override void Up()
    {
        Execute.Sql(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'PublishToFacebook')
            BEGIN
                ALTER TABLE [MarketLocation] ADD [PublishToFacebook] bit NOT NULL DEFAULT 1;
            END

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'PublishToInstagram')
            BEGIN
                ALTER TABLE [MarketLocation] ADD [PublishToInstagram] bit NOT NULL DEFAULT 0;
            END

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'SocialCardRevision')
            BEGIN
                ALTER TABLE [MarketLocation] ADD [SocialCardRevision] int NOT NULL DEFAULT 0;
            END
        ");
    }

    public override void Down()
    {
        Execute.Sql(@"
            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'PublishToFacebook')
            BEGIN
                ALTER TABLE [MarketLocation] DROP COLUMN [PublishToFacebook];
            END

            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'PublishToInstagram')
            BEGIN
                ALTER TABLE [MarketLocation] DROP COLUMN [PublishToInstagram];
            END

            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[MarketLocation]') AND name = 'SocialCardRevision')
            BEGIN
                ALTER TABLE [MarketLocation] DROP COLUMN [SocialCardRevision];
            END
        ");
    }
}
