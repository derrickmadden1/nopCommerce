using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;
using Nop.Plugin.Widgets.SeoEnhancements.Services;
using Nop.Web.Framework.Components;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Widgets.SeoEnhancements.Components;

/// <summary>
/// Injected into head_html_tag. Detects page context from the route model
/// and outputs the appropriate JSON-LD script blocks.
/// </summary>
[ViewComponent(Name = "SeoSchema")]
public class SeoSchemaViewComponent : NopViewComponent
{
    private readonly ISchemaService _schemaService;
    private readonly IFaqService _faqService;

    public SeoSchemaViewComponent(ISchemaService schemaService, IFaqService faqService)
    {
        _schemaService = schemaService;
        _faqService = faqService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        var scripts = new List<string>();

        // Organization schema — always present
        var orgSchema = await _schemaService.BuildOrganizationSchemaAsync();
        if (!string.IsNullOrEmpty(orgSchema))
            scripts.Add(orgSchema);

        // Product page
        if (additionalData is ProductDetailsModel productModel)
        {
            // We need the actual Product entity; resolve via the model's Id
            var productSchema = await _schemaService.BuildProductSchemaAsync(
                new Nop.Core.Domain.Catalog.Product { Id = productModel.Id, Name = productModel.Name,
                    Sku = productModel.Sku ?? string.Empty, Price = productModel.ProductPrice.PriceValue ?? 0,
                    ShortDescription = productModel.ShortDescription });
            if (!string.IsNullOrEmpty(productSchema))
                scripts.Add(productSchema);

            // FAQPage schema if FAQ items exist for this product
            var faqs = await _faqService.GetFaqItemsAsync(SeoFaqEntityType.Product, productModel.Id);
            if (faqs.Any())
            {
                var faqSchema = _schemaService.BuildFaqSchema(faqs);
                if (!string.IsNullOrEmpty(faqSchema))
                    scripts.Add(faqSchema);
            }
        }

        // Category page
        if (additionalData is CategoryModel categoryModel)
        {
            var catSchema = await _schemaService.BuildCategoryBreadcrumbSchemaAsync(
                new Category { Id = categoryModel.Id, Name = categoryModel.Name });
            if (!string.IsNullOrEmpty(catSchema))
                scripts.Add(catSchema);

            var faqs = await _faqService.GetFaqItemsAsync(SeoFaqEntityType.Category, categoryModel.Id);
            if (faqs.Any())
            {
                var faqSchema = _schemaService.BuildFaqSchema(faqs);
                if (!string.IsNullOrEmpty(faqSchema))
                    scripts.Add(faqSchema);
            }
        }

        if (!scripts.Any())
            return Content(string.Empty);

        return View("~/Plugins/Widgets.SeoEnhancements/Views/Components/SeoSchema/Default.cshtml", scripts);
    }
}
