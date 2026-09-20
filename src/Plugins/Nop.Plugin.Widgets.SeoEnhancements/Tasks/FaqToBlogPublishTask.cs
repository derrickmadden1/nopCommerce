using System.Text;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;
using Nop.Plugin.Widgets.SeoEnhancements.Services;
using Nop.Services.Blogs;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Widgets.SeoEnhancements.Tasks;

public class FaqToBlogPublishTask : IScheduleTask
{
    private readonly IFaqService _faqService;
    private readonly IBlogService _blogService;
    private readonly IProductService _productService;
    private readonly ILanguageService _languageService;
    private readonly ILogger _logger;

    public FaqToBlogPublishTask(
        IFaqService faqService,
        IBlogService blogService,
        IProductService productService,
        ILanguageService languageService,
        ILogger logger)
    {
        _faqService = faqService;
        _blogService = blogService;
        _productService = productService;
        _languageService = languageService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var allFaqs = await _faqService.GetAllFaqItemsAsync(publishedOnly: true);
        var productFaqs = allFaqs.Where(x => x.EntityTypeId == (int)SeoFaqEntityType.Product).ToList();

        if (!productFaqs.Any())
            return;

        // Group by product
        var productGroups = productFaqs.GroupBy(x => x.EntityId).ToList();
        
        // Find a product that hasn't been featured in a blog post yet
        // We will just check if a blog post title contains the product name
        var languages = await _languageService.GetAllLanguagesAsync();
        var languageId = languages.FirstOrDefault()?.Id ?? 0;

        foreach (var group in productGroups)
        {
            var product = await _productService.GetProductByIdAsync(group.Key);
            if (product == null || product.Deleted || !product.Published)
                continue;

            var title = $"FAQ of the Week: {product.Name}";
            
            // Check if blog post exists
            var existingPosts = await _blogService.GetAllBlogPostsAsync(showHidden: true, title: title);
            if (existingPosts.Any())
                continue;

            var sb = new StringBuilder();
            sb.AppendLine($"<p>Here are some frequently asked questions about our <strong>{product.Name}</strong>:</p>");
            sb.AppendLine("<dl>");
            
            foreach (var faq in group.OrderBy(x => x.DisplayOrder))
            {
                sb.AppendLine($"<dt><strong>Q: {faq.Question}</strong></dt>");
                sb.AppendLine($"<dd>A: {faq.Answer}</dd>");
            }
            sb.AppendLine("</dl>");

            var blogPost = new BlogPost
            {
                LanguageId = languageId,
                Title = title,
                Body = sb.ToString(),
                BodyOverview = $"Weekly FAQ for {product.Name}",
                AllowComments = true,
                CreatedOnUtc = DateTime.UtcNow,
                StartDateUtc = DateTime.UtcNow,
                IncludeInSitemap = true,
                Tags = "FAQ, " + product.Name
            };

            await _blogService.InsertBlogPostAsync(blogPost);
            await _logger.InformationAsync($"Created weekly FAQ blog post for product: {product.Name}");
            
            // Only create one post per run
            break;
        }
    }
}
