using System.Text;
using System.Text.RegularExpressions;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain;
using Nop.Core.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Helpers;
using Nop.Services.Html;
using Nop.Services.Localization;
using Nop.Services.Topics;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Infrastructure.Cache;

namespace Nop.Web.Factories;

/// <summary>
/// Represents the factory to prepare llms.txt content
/// </summary>
public partial class LlmsModelFactory : ILlmsModelFactory
{
    #region Fields

    protected readonly ICategoryService _categoryService;
    protected readonly IHtmlFormatter _htmlFormatter;
    protected readonly ILocalizationService _localizationService;
    protected readonly INopFileProvider _fileProvider;
    protected readonly INopUrlHelper _nopUrlHelper;
    protected readonly IProductService _productService;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IStoreContext _storeContext;
    protected readonly ITopicService _topicService;
    protected readonly IWebHelper _webHelper;
    protected readonly IWorkContext _workContext;
    protected readonly StoreInformationSettings _storeInformationSettings;

    #endregion

    #region Ctor

    public LlmsModelFactory(ICategoryService categoryService,
        IHtmlFormatter htmlFormatter,
        ILocalizationService localizationService,
        INopFileProvider fileProvider,
        INopUrlHelper nopUrlHelper,
        IProductService productService,
        IStaticCacheManager staticCacheManager,
        IStoreContext storeContext,
        ITopicService topicService,
        IWebHelper webHelper,
        IWorkContext workContext,
        StoreInformationSettings storeInformationSettings)
    {
        _categoryService = categoryService;
        _htmlFormatter = htmlFormatter;
        _localizationService = localizationService;
        _fileProvider = fileProvider;
        _nopUrlHelper = nopUrlHelper;
        _productService = productService;
        _staticCacheManager = staticCacheManager;
        _storeContext = storeContext;
        _topicService = topicService;
        _webHelper = webHelper;
        _workContext = workContext;
        _storeInformationSettings = storeInformationSettings;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Ensure URL is an absolute URL
    /// </summary>
    /// <param name="url">URL to verify</param>
    /// <param name="storeLocation">Store location</param>
    /// <returns>Absolute URL</returns>
    protected virtual string EnsureAbsoluteUrl(string url, string storeLocation)
    {
        if (string.IsNullOrWhiteSpace(url))
            return storeLocation;

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        return $"{storeLocation.TrimEnd('/')}/{url.TrimStart('/')}";
    }

    /// <summary>
    /// Clean and truncate text for markdown summary lines
    /// </summary>
    /// <param name="text">Input text</param>
    /// <param name="maxLength">Maximum length</param>
    /// <returns>Cleaned plain text</returns>
    protected virtual string CleanDescription(string text, int maxLength = 160)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var plain = _htmlFormatter.ConvertHtmlToPlainText(text, decode: true, replaceAnchorTags: true);
        plain = _htmlFormatter.StripTags(plain);

        // Normalize whitespaces and line breaks
        plain = Regex.Replace(plain, @"\s+", " ").Trim();

        // Escape square brackets to avoid broken markdown link syntax
        plain = plain.Replace("[", "(").Replace("]", ")");

        if (plain.Length > maxLength)
            plain = plain[..maxLength].TrimEnd() + "...";

        return plain;
    }

    /// <summary>
    /// Get topic description based on system name or title
    /// </summary>
    /// <param name="systemName">Topic system name</param>
    /// <param name="title">Topic title</param>
    /// <returns>Description</returns>
    protected virtual string GetTopicDescription(string systemName, string title)
    {
        if (string.Equals(systemName, "AboutUs", StringComparison.OrdinalIgnoreCase))
            return "Learn about our store, mission, and background.";

        if (string.Equals(systemName, "PrivacyInfo", StringComparison.OrdinalIgnoreCase))
            return "Review our privacy policy and data protection practices.";

        if (string.Equals(systemName, "ShippingInfo", StringComparison.OrdinalIgnoreCase))
            return "Details on shipping rates, delivery times, and fulfillment policies.";

        if (string.Equals(systemName, "ConditionsOfUse", StringComparison.OrdinalIgnoreCase))
            return "Terms and conditions of use for our online store.";

        if (string.Equals(systemName, "ContactUs", StringComparison.OrdinalIgnoreCase))
            return "Contact our support team for inquiries and help.";

        return $"Read details about {title}.";
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepare llms.txt content
    /// </summary>
    /// <param name="full">A value indicating whether to generate full content (llms-full.txt)</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the llms.txt content as string
    /// </returns>
    public virtual async Task<string> PrepareLlmsTextFileAsync(bool full = false)
    {
        // Check for custom override file in wwwroot
        var customFileName = full ? "llms-full.custom.txt" : "llms.custom.txt";
        var customFilePath = _fileProvider.Combine(_fileProvider.MapPath("~/wwwroot"), customFileName);
        if (_fileProvider.FileExists(customFilePath))
        {
            return await _fileProvider.ReadAllTextAsync(customFilePath, Encoding.UTF8);
        }

        var store = await _storeContext.GetCurrentStoreAsync();
        var currentLanguage = await _workContext.GetWorkingLanguageAsync();

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(
            NopModelCacheDefaults.LlmsTextModelKey,
            store.Id,
            currentLanguage.Id,
            full);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var sb = new StringBuilder();
            var storeName = !string.IsNullOrWhiteSpace(store.Name) ? store.Name : "Online Store";
            var storeLocation = _webHelper.GetStoreLocation();
            var protocol = _webHelper.GetCurrentRequestProtocol();

            // 1. Markdown H1 Header (required as first element by llms.txt specification)
            sb.AppendLine(full ? $"# {storeName} - Full Site Catalog" : $"# {storeName}");
            sb.AppendLine();

            // 2. Blockquote summary (must exceed 50 characters to satisfy Lighthouse Agentic Browsing audit)
            var storeSummary = $"Welcome to {storeName}. Discover our online catalog featuring curated products, categories, and dedicated customer services.";
            if (storeSummary.Length < 60)
                storeSummary = $"Welcome to {storeName}, an online boutique offering quality products, seamless shopping, and customer assistance.";
            sb.AppendLine($"> {storeSummary}");
            sb.AppendLine();

            // 3. Optional context and guidance for AI agents
            sb.AppendLine("This file provides a structured directory for AI agents, LLMs, and automated crawlers to discover and navigate store offerings, categories, products, and policies.");
            sb.AppendLine();
            sb.AppendLine($"- **Base URL:** {storeLocation}");
            sb.AppendLine($"- **Search Endpoint:** `{storeLocation}search?q={{query}}`");
            sb.AppendLine("- **Authentication:** Browsing the catalog and viewing published products requires no authentication.");
            sb.AppendLine();

            // 4. Categories Section
            var categories = await _categoryService.GetAllCategoriesAsync(storeId: store.Id);
            var publishedCategories = categories.Where(c => c.Published).ToList();
            if (publishedCategories.Any())
            {
                sb.AppendLine("## Categories");
                sb.AppendLine();
                foreach (var category in publishedCategories)
                {
                    var catName = await _localizationService.GetLocalizedAsync(category, x => x.Name);
                    if (string.IsNullOrWhiteSpace(catName))
                        continue;

                    var rawCatUrl = await _nopUrlHelper.RouteGenericUrlAsync(category, protocol);
                    var catUrl = EnsureAbsoluteUrl(rawCatUrl, storeLocation);

                    var catDesc = CleanDescription(await _localizationService.GetLocalizedAsync(category, x => x.Description), 140);
                    if (string.IsNullOrWhiteSpace(catDesc))
                        catDesc = $"Browse {catName} products and collections.";

                    sb.AppendLine($"- [{catName}]({catUrl}): {catDesc}");
                }
                sb.AppendLine();
            }

            // 5. Products Section
            var pageSize = full ? int.MaxValue : 50;
            var products = await _productService.SearchProductsAsync(
                pageIndex: 0,
                pageSize: pageSize,
                storeId: store.Id,
                visibleIndividuallyOnly: true);

            if (products.Any())
            {
                sb.AppendLine(full ? "## Product Catalog" : "## Featured Products");
                sb.AppendLine();
                foreach (var product in products)
                {
                    var prodName = await _localizationService.GetLocalizedAsync(product, x => x.Name);
                    if (string.IsNullOrWhiteSpace(prodName))
                        continue;

                    var rawProdUrl = await _nopUrlHelper.RouteGenericUrlAsync(product, protocol);
                    var prodUrl = EnsureAbsoluteUrl(rawProdUrl, storeLocation);

                    var prodDesc = CleanDescription(await _localizationService.GetLocalizedAsync(product, x => x.ShortDescription), 140);
                    if (string.IsNullOrWhiteSpace(prodDesc))
                        prodDesc = $"View {prodName} details, availability, and pricing.";

                    sb.AppendLine($"- [{prodName}]({prodUrl}): {prodDesc}");
                }
                sb.AppendLine();
            }

            // 6. Topics and Store Information Section
            var topics = await _topicService.GetAllTopicsAsync(storeId: store.Id);
            var publishedTopics = topics
                .Where(t => t.Published && !t.IsPasswordProtected)
                .Where(t => t.IncludeInSitemap ||
                            string.Equals(t.SystemName, "AboutUs", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.SystemName, "PrivacyInfo", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.SystemName, "ShippingInfo", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.SystemName, "ConditionsOfUse", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (publishedTopics.Any())
            {
                sb.AppendLine("## Store Information & Policies");
                sb.AppendLine();
                foreach (var topic in publishedTopics)
                {
                    var title = await _localizationService.GetLocalizedAsync(topic, x => x.Title);
                    if (string.IsNullOrWhiteSpace(title))
                        title = topic.SystemName;

                    var rawTopicUrl = await _nopUrlHelper.RouteGenericUrlAsync(topic, protocol);
                    var topicUrl = EnsureAbsoluteUrl(rawTopicUrl, storeLocation);
                    var topicDesc = GetTopicDescription(topic.SystemName, title);

                    sb.AppendLine($"- [{title}]({topicUrl}): {topicDesc}");
                }
                sb.AppendLine();
            }

            // 7. Navigation & Endpoints Section
            sb.AppendLine("## Navigation & Endpoints");
            sb.AppendLine();
            sb.AppendLine($"- [Store Homepage]({storeLocation}): Main storefront featuring latest products and collections.");
            sb.AppendLine($"- [Product Search]({storeLocation}search?q={{query}}): Search all products across the catalog by keywords.");
            sb.AppendLine($"- [Contact Us]({storeLocation}contactus): Inquire about orders, customer support, or general questions.");
            sb.AppendLine($"- [Shopping Cart]({storeLocation}cart): Review current shopping cart items and proceed to checkout.");
            sb.AppendLine();

            // 8. Optional Section
            sb.AppendLine("## Optional");
            sb.AppendLine();
            sb.AppendLine($"- [Full Site Content]({storeLocation}llms-full.txt): Comprehensive site content and extended product catalog for offline LLM ingestion.");

            return sb.ToString();
        });
    }

    #endregion
}
