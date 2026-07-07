using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;

namespace Nop.Plugin.Widgets.AiRecommendations.Data;

public class ProductEmbeddingRecordMap : NopEntityBuilder<ProductEmbeddingRecord>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(ProductEmbeddingRecord.ProductId)).AsInt32().NotNullable()
            .WithColumn(nameof(ProductEmbeddingRecord.EmbeddingJson)).AsCustom("NVARCHAR(MAX)").NotNullable()
            .WithColumn(nameof(ProductEmbeddingRecord.ContentHash)).AsString(32).NotNullable()
            .WithColumn(nameof(ProductEmbeddingRecord.GeneratedAtUtc)).AsDateTime2().NotNullable();
    }
}
