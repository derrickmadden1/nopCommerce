using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.MarketLocator.Domain;

namespace Nop.Plugin.Widgets.MarketLocator.Data;

[NopSchemaMigration("2024/01/01 00:00:00:0001", "Widgets.MarketLocator base schema")]
public class SchemaMigration : AutoReversingMigration
{
    public override void Up()
    {
        Create.TableFor<MarketLocation>();
    }
}
