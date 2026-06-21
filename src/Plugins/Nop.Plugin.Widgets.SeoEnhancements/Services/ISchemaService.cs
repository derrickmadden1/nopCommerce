using Nop.Core.Domain.Catalog;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;

namespace Nop.Plugin.Widgets.SeoEnhancements.Services;

public interface ISchemaService
{
    /// <summary>Builds Product + BreadcrumbList JSON-LD for a product detail page.</summary>
    Task<string> BuildProductSchemaAsync(Product product);

    /// <summary>Builds BreadcrumbList JSON-LD for a category page.</summary>
    Task<string> BuildCategoryBreadcrumbSchemaAsync(Category category);

    /// <summary>Builds FAQPage JSON-LD from a list of FAQ items.</summary>
    string BuildFaqSchema(IList<SeoFaqItem> faqs);

    /// <summary>Builds Organization JSON-LD for the store.</summary>
    Task<string> BuildOrganizationSchemaAsync();
}
