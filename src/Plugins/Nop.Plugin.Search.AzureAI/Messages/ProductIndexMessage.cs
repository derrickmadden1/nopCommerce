namespace Nop.Plugin.Search.AzureAI.Messages;

/// <summary>
/// Self-contained message contract — carries all product data needed to index.
/// Duplicate this class into your Azure Functions project — keep both in sync.
/// </summary>
public class ProductIndexMessage
{
    public int ProductId { get; set; }
    public ProductIndexAction Action { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    // Full product data — only populated for Index actions
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? FullDescription { get; set; }
    public string? Sku { get; set; }
    public decimal Price { get; set; }
    public bool Published { get; set; }
    public List<string> CategoryNames { get; set; } = new();
    public List<string> ManufacturerNames { get; set; } = new();
}

public enum ProductIndexAction
{
    Index,
    Delete
}
