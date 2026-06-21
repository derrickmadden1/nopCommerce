using Nop.Core;

namespace Nop.Plugin.Widgets.SeoEnhancements.Domain;

/// <summary>
/// Represents an FAQ entry attached to a product or category.
/// EntityTypeId: 1 = Product, 2 = Category
/// </summary>
public class SeoFaqItem : BaseEntity
{
    public int EntityTypeId { get; set; }
    public int EntityId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool Published { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}

public enum SeoFaqEntityType
{
    Product = 1,
    Category = 2
}
