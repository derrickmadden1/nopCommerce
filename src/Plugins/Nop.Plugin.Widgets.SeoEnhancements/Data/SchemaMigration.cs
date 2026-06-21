using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;

namespace Nop.Plugin.Widgets.SeoEnhancements.Data;

[NopMigration("2025-01-01 00:00:00", "Widgets.SeoEnhancements - SeoFaqItem schema", MigrationProcessType.Installation)]
public class SchemaMigration : AutoReversingMigration
{
    public override void Up()
    {
        this.CreateTableIfNotExists<SeoFaqItem>();
    }
}
