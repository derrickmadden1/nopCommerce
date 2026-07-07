using Nop.Core;

namespace Nop.Plugin.Widgets.AiRecommendations.Data;

/// <summary>
/// Stores a product's embedding vector in SQL.
/// Custom table: AiRecommendations_ProductEmbedding
/// </summary>
public class ProductEmbeddingRecord : BaseEntity
{
    public int ProductId { get; set; }

    /// <summary>
    /// Embedding vector serialised as a JSON float array
    /// </summary>
    public string EmbeddingJson { get; set; } = string.Empty;

    /// <summary>
    /// MD5 of the content that was embedded — used to skip re-embedding unchanged products
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }
}
