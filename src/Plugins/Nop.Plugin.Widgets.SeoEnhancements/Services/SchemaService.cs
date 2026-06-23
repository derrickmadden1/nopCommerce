using System.Text;
using System.Text.Json;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Services.Catalog;
using Nop.Services.Helpers;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Services.Stores;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;

namespace Nop.Plugin.Widgets.SeoEnhancements.Services;

public class SchemaService : ISchemaService
{
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;
    private readonly IWebHelper _webHelper;
    private readonly IPictureService _pictureService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly ICategoryService _categoryService;
    private readonly IProductService _productService;
    private readonly MediaSettings _mediaSettings;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public SchemaService(
        IStoreContext storeContext,
        IStoreService storeService,
        IWebHelper webHelper,
        IPictureService pictureService,
        IUrlRecordService urlRecordService,
        ICategoryService categoryService,
        IProductService productService,
        MediaSettings mediaSettings)
    {
        _storeContext = storeContext;
        _storeService = storeService;
        _webHelper = webHelper;
        _pictureService = pictureService;
        _urlRecordService = urlRecordService;
        _categoryService = categoryService;
        _productService = productService;
        _mediaSettings = mediaSettings;
    }

    public async Task<string> BuildProductSchemaAsync(Product product)
    {
        var storeUrl = _webHelper.GetStoreLocation().TrimEnd('/');
        var slug = await _urlRecordService.GetSeNameAsync(product);
        var productUrl = $"{storeUrl}/{slug}";

        // Get main product image
        string? imageUrl = null;
        var pictures = await _pictureService.GetPicturesByProductIdAsync(product.Id, 1);
        if (pictures.Any())
            (imageUrl, _) = await _pictureService.GetPictureUrlAsync(pictures[0], _mediaSettings.ProductDetailsPictureSize);

        // Build breadcrumb from category hierarchy
        var breadcrumbs = await BuildProductBreadcrumbsAsync(product, storeUrl);

        var productSchema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = product.Name,
            ["description"] = StripHtml(product.ShortDescription ?? product.FullDescription ?? string.Empty),
            ["sku"] = product.Sku,
            ["url"] = productUrl,
            ["image"] = imageUrl,
            ["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["url"] = productUrl,
                ["priceCurrency"] = "GBP", // Override per your store
                ["price"] = product.Price.ToString("F2"),
                ["availability"] = product.StockQuantity > 0 || !product.ManageInventoryMethodId.Equals(1)
                    ? "https://schema.org/InStock"
                    : "https://schema.org/OutOfStock",
                ["itemCondition"] = "https://schema.org/NewCondition"
            }
        };

        var breadcrumbSchema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = breadcrumbs
        };

        return SerializeGraph(productSchema, breadcrumbSchema);
    }

    public async Task<string> BuildCategoryBreadcrumbSchemaAsync(Category category)
    {
        var storeUrl = _webHelper.GetStoreLocation().TrimEnd('/');

        var breadcrumbs = await BuildCategoryBreadcrumbsAsync(category, storeUrl);

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = breadcrumbs
        };

        return SerializeGraph(schema);
    }

    public string BuildFaqSchema(IList<SeoFaqItem> faqs)
    {
        if (!faqs.Any())
            return string.Empty;

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = faqs.Select(f => new Dictionary<string, object?>
            {
                ["@type"] = "Question",
                ["name"] = f.Question,
                ["acceptedAnswer"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Answer",
                    ["text"] = StripHtml(f.Answer)
                }
            }).ToList()
        };

        return SerializeGraph(schema);
    }

    public async Task<string> BuildOrganizationSchemaAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var storeUrl = _webHelper.GetStoreLocation().TrimEnd('/');

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Organization",
            ["name"] = store.Name,
            ["url"] = storeUrl,
            ["logo"] = new Dictionary<string, object?>
            {
                ["@type"] = "ImageObject",
                ["url"] = $"{storeUrl}/images/thumbs/0000000_Rose-Cottage-Croft-logo.png" // Update to your actual logo path
            },
            ["contactPoint"] = new Dictionary<string, object?>
            {
                ["@type"] = "ContactPoint",
                ["contactType"] = "customer service",
                ["availableLanguage"] = "English"
            }
        };

        return SerializeGraph(schema);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<List<Dictionary<string, object?>>> BuildProductBreadcrumbsAsync(Product product, string storeUrl)
    {
        var items = new List<Dictionary<string, object?>>
        {
            BreadcrumbItem(1, "Home", storeUrl)
        };

        var categories = await _categoryService.GetProductCategoriesByProductIdAsync(product.Id);
        if (categories.Any())
        {
            var cat = await _categoryService.GetCategoryByIdAsync(categories[0].CategoryId);
            if (cat != null)
            {
                var catSlug = await _urlRecordService.GetSeNameAsync(cat);
                items.Add(BreadcrumbItem(2, cat.Name, $"{storeUrl}/{catSlug}"));
            }
        }

        var productSlug = await _urlRecordService.GetSeNameAsync(product);
        items.Add(BreadcrumbItem(items.Count + 1, product.Name, $"{storeUrl}/{productSlug}"));

        return items;
    }

    private async Task<List<Dictionary<string, object?>>> BuildCategoryBreadcrumbsAsync(Category category, string storeUrl)
    {
        var items = new List<Dictionary<string, object?>>
        {
            BreadcrumbItem(1, "Home", storeUrl)
        };

        // Walk up the category tree for nested categories
        var ancestors = new Stack<Category>();
        var current = category;
        while (current.ParentCategoryId > 0)
        {
            var parent = await _categoryService.GetCategoryByIdAsync(current.ParentCategoryId);
            if (parent == null) break;
            ancestors.Push(parent);
            current = parent;
        }

        var position = 2;
        foreach (var ancestor in ancestors)
        {
            var slug = await _urlRecordService.GetSeNameAsync(ancestor);
            items.Add(BreadcrumbItem(position++, ancestor.Name, $"{storeUrl}/{slug}"));
        }

        var catSlug = await _urlRecordService.GetSeNameAsync(category);
        items.Add(BreadcrumbItem(position, category.Name, $"{storeUrl}/{catSlug}"));

        return items;
    }

    private static Dictionary<string, object?> BreadcrumbItem(int position, string name, string url)
    {
        return new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = position,
            ["name"] = name,
            ["item"] = url
        };
    }

    /// <summary>Wraps one or more schema objects in a @graph if multiple, else returns single.</summary>
    private static string SerializeGraph(params Dictionary<string, object?>[] schemas)
    {
        if (schemas.Length == 1)
            return JsonSerializer.Serialize(schemas[0], _jsonOptions);

        var graph = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = schemas.Select(s =>
            {
                // Remove @context from children when wrapped in @graph
                var copy = new Dictionary<string, object?>(s);
                copy.Remove("@context");
                return copy;
            }).ToList()
        };

        return JsonSerializer.Serialize(graph, _jsonOptions);
    }

    private static string StripHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty).Trim();
    }
}
