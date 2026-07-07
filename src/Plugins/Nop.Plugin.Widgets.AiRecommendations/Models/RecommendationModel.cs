using Nop.Core.Domain.Catalog;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.AiRecommendations.Models;

public record RecommendationModel : BaseNopModel
{
    public string Title { get; set; } = string.Empty;
    public IList<RecommendedProduct> Products { get; set; } = new List<RecommendedProduct>();
}

public record RecommendedProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SeName { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public string Price { get; set; } = string.Empty;
}
