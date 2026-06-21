using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;

namespace Nop.Plugin.Widgets.SeoEnhancements.Data;

public class SeoFaqItemBuilder : NopEntityBuilder<SeoFaqItem>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(SeoFaqItem.EntityTypeId)).AsInt32().NotNullable()
            .WithColumn(nameof(SeoFaqItem.EntityId)).AsInt32().NotNullable()
            .WithColumn(nameof(SeoFaqItem.Question)).AsString(500).NotNullable()
            .WithColumn(nameof(SeoFaqItem.Answer)).AsString(int.MaxValue).NotNullable()
            .WithColumn(nameof(SeoFaqItem.DisplayOrder)).AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn(nameof(SeoFaqItem.Published)).AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn(nameof(SeoFaqItem.CreatedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(SeoFaqItem.UpdatedOnUtc)).AsDateTime2().NotNullable();
    }
}
