using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Widgets.SeoEnhancements.Models;

public record FaqItemModel : BaseNopEntityModel
{
    public int EntityTypeId { get; set; }
    public int EntityId { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.SeoEnhancements.FAQ.Question")]
    [Required]
    [MaxLength(500)]
    public string Question { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.SeoEnhancements.FAQ.Answer")]
    [Required]
    public string Answer { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.SeoEnhancements.FAQ.DisplayOrder")]
    public int DisplayOrder { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.SeoEnhancements.FAQ.Published")]
    public bool Published { get; set; } = true;

    // Display labels for the admin list
    public string EntityTypeName => EntityTypeId == 1 ? "Product" : "Category";
    public string EntityName { get; set; } = string.Empty;
}

public record FaqItemListModel : BasePagedListModel<FaqItemModel>
{
}

public record FaqItemSearchModel : BaseSearchModel
{
    public int? EntityTypeId { get; set; }
    public int? EntityId { get; set; }
}
