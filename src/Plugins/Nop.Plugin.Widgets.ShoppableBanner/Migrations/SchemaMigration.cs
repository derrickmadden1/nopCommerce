using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.ShoppableBanner.Domains;

namespace Nop.Plugin.Widgets.ShoppableBanner.Migrations;

[NopMigration("2026/06/09 09:51:10", "Nop.Plugin.Widgets.ShoppableBanner schema", MigrationProcessType.Installation)]
public class SchemaMigration : AutoReversingMigration
{
    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        this.CreateTableIfNotExists<CustomTable>();
    }
}