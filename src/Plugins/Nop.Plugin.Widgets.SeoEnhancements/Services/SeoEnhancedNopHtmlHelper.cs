using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Hosting;
using Nop.Core;
using Nop.Core.Configuration;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Seo;
using Nop.Services.Catalog;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Seo;
using Nop.Web.Framework.UI;
using Nop.Web.Framework.WebOptimizer;
using System;

namespace Nop.Plugin.Widgets.SeoEnhancements.Services;

public class SeoEnhancedNopHtmlHelper : NopHtmlHelper
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public SeoEnhancedNopHtmlHelper(
        AppSettings appSettings,
        HtmlEncoder htmlEncoder,
        IHtmlHelper htmlHelper,
        IHttpContextAccessor httpContextAccessor,
        INopAssetHelper bundleHelper,
        Lazy<ILocalizationService> localizationService,
        IStoreContext storeContext,
        IWebHelper webHelper,
        IWebHostEnvironment webHostEnvironment,
        SeoSettings seoSettings,
        IProductService productService,
        ICategoryService categoryService)
        : base(appSettings, htmlEncoder, htmlHelper, httpContextAccessor, bundleHelper, localizationService, storeContext, webHelper, webHostEnvironment, seoSettings)
    {
        _productService = productService;
        _categoryService = categoryService;
    }

    public override async Task<IHtmlContent> GenerateMetaDescriptionAsync(string part = "")
    {
        var enhanced = await TryBuildEnhancedDescriptionAsync();
        if (!string.IsNullOrWhiteSpace(enhanced))
        {
            // Override/append whatever was set by the controller
            AppendMetaDescriptionParts(enhanced);
        }

        return await base.GenerateMetaDescriptionAsync(part);
    }

    private async Task<string?> TryBuildEnhancedDescriptionAsync()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return null;

        // Product pages: /product-slug or /p/123/slug
        var productId = TryExtractProductId(ctx);
        if (productId.HasValue)
        {
            var product = await _productService.GetProductByIdAsync(productId.Value);
            if (product != null)
                return BuildProductDescription(product);
        }

        // Category pages
        var categoryId = TryExtractCategoryId(ctx);
        if (categoryId.HasValue)
        {
            var category = await _categoryService.GetCategoryByIdAsync(categoryId.Value);
            if (category != null)
                return BuildCategoryDescription(category);
        }

        return null;
    }

    private static string BuildProductDescription(Product product)
    {
        var core = StripHtml(product.ShortDescription ?? product.FullDescription ?? string.Empty);
        if (core.Length > 120)
            core = core[..117] + "...";

        return string.IsNullOrWhiteSpace(core)
            ? $"Buy {product.Name} online. Quality products from Rose Cottage Croft."
            : $"Buy {product.Name} – {core}";
    }

    private static string BuildCategoryDescription(Category category)
    {
        var core = StripHtml(category.Description ?? string.Empty);
        if (core.Length > 130)
            core = core[..127] + "...";

        return string.IsNullOrWhiteSpace(core)
            ? $"Shop {category.Name} at Rose Cottage Croft. Browse our full range online."
            : $"Shop {category.Name} – {core}";
    }

    private static int? TryExtractProductId(HttpContext ctx)
    {
        if (ctx.Request.RouteValues.TryGetValue("productId", out var val) &&
            int.TryParse(val?.ToString(), out var id))
            return id;
        return null;
    }

    private static int? TryExtractCategoryId(HttpContext ctx)
    {
        if (ctx.Request.RouteValues.TryGetValue("categoryId", out var val) &&
            int.TryParse(val?.ToString(), out var id))
            return id;
        return null;
    }

    private static string StripHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty).Trim();
    }
}
