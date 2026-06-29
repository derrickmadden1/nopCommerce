using FluentMigrator;
using Nop.Data;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Feed.GoogleShopping.Domain;
using Nop.Web.Framework.Extensions;

namespace Nop.Plugin.Feed.GoogleShopping.Data;

[NopMigration("2026-06-29 12:00:00", "Feed.GoogleShopping 1.10. Add UseShortDescription column", MigrationProcessType.Update)]
public class AddUseShortDescriptionMigration : MigrationBase
{
    #region Methods

    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        var tableName = NameCompatibilityManager.GetTableName(typeof(GoogleProductRecord));

        // add UseShortDescription column if not exists
        if (!Schema.Table(tableName).Column(nameof(GoogleProductRecord.UseShortDescription)).Exists())
        {
            Alter.Table(tableName)
                .AddColumn(nameof(GoogleProductRecord.UseShortDescription))
                .AsBoolean().NotNullable().SetExistingRowsTo(false);
        }

        // locales
        this.AddOrUpdateLocaleResource(new Dictionary<string, string>
        {
            ["Plugins.Feed.GoogleShopping.Products.UseShortDescription"] = "Use short description",
            ["Plugins.Feed.GoogleShopping.Products.UseShortDescription.Hint"] = "Check to use the product's short description instead of the full description in the feed."
        });
    }

    /// <summary>
    /// Collects the DOWN migration expressions
    /// </summary>
    public override void Down()
    {
        // nothing
    }

    #endregion
}
