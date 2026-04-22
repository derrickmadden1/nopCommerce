using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.ImagePuzzle.Domains;

namespace Nop.Plugin.Widgets.ImagePuzzle.Migrations;

[NopMigration("2026-03-19 21:13:33", "Nop.Plugin.Widgets.ImagePuzzle schema", MigrationProcessType.Installation)]
public class SchemaMigration : Migration
{
    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        this.CreateTableIfNotExists<CustomTable>();
    }

    public override void Down()
    {
        this.DeleteTableIfExists<CustomTable>();
    }
}