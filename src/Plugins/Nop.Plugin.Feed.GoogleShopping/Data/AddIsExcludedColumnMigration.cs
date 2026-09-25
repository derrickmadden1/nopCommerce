using FluentMigrator;
using Nop.Data;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.Feed.GoogleShopping.Domain;
using Nop.Web.Framework.Extensions;

namespace Nop.Plugin.Feed.GoogleShopping.Data;

[NopMigration("2026-09-25 10:00:00", "Feed.GoogleShopping 1.11. Add IsExcluded column", MigrationProcessType.Update)]
public class AddIsExcludedColumnMigration : MigrationBase
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

        if (!Schema.Table(tableName).Exists())
            return;

        // add IsExcluded column if not exists
        if (!Schema.Table(tableName).Column(nameof(GoogleProductRecord.IsExcluded)).Exists())
        {
            Alter.Table(tableName)
                .AddColumn(nameof(GoogleProductRecord.IsExcluded))
                .AsBoolean().NotNullable().SetExistingRowsTo(false);
        }

        // locales
        this.AddOrUpdateLocaleResource(new Dictionary<string, string>
        {
            ["Plugins.Feed.GoogleShopping.Products.IsExcluded"] = "Exclude from feed",
            ["Plugins.Feed.GoogleShopping.Products.IsExcluded.Hint"] = "Check to exclude this product from the Google Shopping feed."
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
