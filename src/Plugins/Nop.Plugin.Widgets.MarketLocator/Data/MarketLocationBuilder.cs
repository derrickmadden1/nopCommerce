using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Widgets.MarketLocator.Domain;

namespace Nop.Plugin.Widgets.MarketLocator.Data;

public class MarketLocationBuilder : NopEntityBuilder<MarketLocation>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(MarketLocation.Name)).AsString(200).NotNullable()
            .WithColumn(nameof(MarketLocation.Address)).AsString(500).NotNullable()
            .WithColumn(nameof(MarketLocation.City)).AsString(100).NotNullable()
            .WithColumn(nameof(MarketLocation.Latitude)).AsDecimal(10, 7).NotNullable()
            .WithColumn(nameof(MarketLocation.Longitude)).AsDecimal(10, 7).NotNullable()
            .WithColumn(nameof(MarketLocation.Hours)).AsString(100).NotNullable()
            .WithColumn(nameof(MarketLocation.UpcomingDates)).AsString(2000).NotNullable()
            .WithColumn(nameof(MarketLocation.Frequency)).AsString(50).NotNullable()
            .WithColumn(nameof(MarketLocation.Status)).AsString(20).NotNullable()
            .WithColumn(nameof(MarketLocation.Published)).AsBoolean().NotNullable()
            .WithColumn(nameof(MarketLocation.DisplayOrder)).AsInt32().NotNullable();
    }
}
