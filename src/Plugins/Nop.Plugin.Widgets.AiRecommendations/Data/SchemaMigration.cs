using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;

namespace Nop.Plugin.Widgets.AiRecommendations.Data;

[NopSchemaMigration("2024/01/01 00:00:00:0000000", "AiRecommendations.ProductEmbedding base schema")]
public class SchemaMigration : AutoReversingMigration
{
    public override void Up()
    {
        this.CreateTableIfNotExists<ProductEmbeddingRecord>();
    }
}
