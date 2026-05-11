using FluentMigrator;
using Nop.Data.Migrations;

namespace Nop.Plugin.Widgets.MarketLocator.Data;

[NopSchemaMigration("2026-04-15 15:00:00", "Widgets.MarketLocator add missing localization resources")]
public class AddMissingLocalizationResources : Migration
{
    public override void Up()
    {
        // Add resources for all languages
        Execute.Sql(@"
            INSERT INTO [LocaleStringResource] ([LanguageId], [ResourceName], [ResourceValue])
            SELECT l.Id, 'Plugins.Widgets.MarketLocator.Settings.EnableSocialPublishing', 'Enable Social Publishing'
            FROM [Language] l
            WHERE NOT EXISTS (SELECT 1 FROM [LocaleStringResource] lsr WHERE lsr.LanguageId = l.Id AND lsr.ResourceName = 'Plugins.Widgets.MarketLocator.Settings.EnableSocialPublishing')

            INSERT INTO [LocaleStringResource] ([LanguageId], [ResourceName], [ResourceValue])
            SELECT l.Id, 'Plugins.Widgets.MarketLocator.Settings.SocialPublishDaysBeforeMarket', 'Publish Days Before Market'
            FROM [Language] l
            WHERE NOT EXISTS (SELECT 1 FROM [LocaleStringResource] lsr WHERE lsr.LanguageId = l.Id AND lsr.ResourceName = 'Plugins.Widgets.MarketLocator.Settings.SocialPublishDaysBeforeMarket')

            INSERT INTO [LocaleStringResource] ([LanguageId], [ResourceName], [ResourceValue])
            SELECT l.Id, 'Plugins.Widgets.MarketLocator.Settings.StoreUrl', 'Store URL'
            FROM [Language] l
            WHERE NOT EXISTS (SELECT 1 FROM [LocaleStringResource] lsr WHERE lsr.LanguageId = l.Id AND lsr.ResourceName = 'Plugins.Widgets.MarketLocator.Settings.StoreUrl')
        ");
    }

    public override void Down()
    {
        // Optional: remove them if needed, but usually we leave them
    }
}
