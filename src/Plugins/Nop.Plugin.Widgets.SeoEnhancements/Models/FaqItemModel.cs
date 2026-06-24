using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    [NopResourceDisplayName("Admin.Catalog.Products.List.SearchProductName")]
    public string SearchKeywords { get; set; } = string.Empty;

    [NopResourceDisplayName("Admin.Catalog.Products.List.SearchCategory")]
    public int SearchCategoryId { get; set; }

    [NopResourceDisplayName("Admin.Catalog.Products.List.SearchPublished")]
    public int SearchPublishedId { get; set; }

    public IList<SelectListItem> AvailableCategories { get; set; } = new List<SelectListItem>();

    public IList<SelectListItem> AvailablePublishedOptions { get; set; } = new List<SelectListItem>();
}
