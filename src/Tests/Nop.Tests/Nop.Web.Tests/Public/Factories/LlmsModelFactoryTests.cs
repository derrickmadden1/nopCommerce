using AwesomeAssertions;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Stores;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Web.Factories;
using Nop.Web.Infrastructure.Cache;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Public.Factories;

[TestFixture]
public class LlmsModelFactoryTests : BaseNopTest
{
    private ILlmsModelFactory _llmsModelFactory;
    private IStaticCacheManager _staticCacheManager;
    private IProductService _productService;
    private IRepository<Product> _productRepository;
    private IRepository<Store> _storeRepository;

    [OneTimeSetUp]
    public void SetUp()
    {
        _llmsModelFactory = GetService<ILlmsModelFactory>();
        _staticCacheManager = GetService<IStaticCacheManager>();
        _productService = GetService<IProductService>();
        _productRepository = GetService<IRepository<Product>>();
        _storeRepository = GetService<IRepository<Store>>();
    }

    [Test]
    public async Task CanPrepareLlmsTextFileAsync()
    {
        var content = await _llmsModelFactory.PrepareLlmsTextFileAsync(full: false);

        content.Should().NotBeNullOrWhiteSpace();

        // 1. Starts with Markdown H1 header
        content.Should().StartWith("# ");

        // 2. Contains blockquote summary with at least 50 characters (Lighthouse Agentic Browsing requirement)
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var blockquoteLine = lines.FirstOrDefault(l => l.StartsWith("> "));
        blockquoteLine.Should().NotBeNull();
        blockquoteLine.TrimStart('>', ' ').Length.Should().BeGreaterThanOrEqualTo(50);

        // 3. Contains properly formatted Markdown links: [Title](URL): Description
        content.Should().Contain("- [");
        content.Should().Contain("](");
        content.Should().Contain("): ");

        // 4. Contains required sections
        content.Should().Contain("## Navigation & Endpoints");
        content.Should().Contain("## Optional");
        content.Should().Contain("/llms-full.txt");
    }

    [Test]
    public async Task CanPrepareLlmsFullTextFileAsync()
    {
        var content = await _llmsModelFactory.PrepareLlmsTextFileAsync(full: true);

        content.Should().NotBeNullOrWhiteSpace();
        content.Should().StartWith("# ");
        content.Should().Contain("Full Site Catalog");
        content.Should().Contain("- [");
        content.Should().Contain("](");
    }

    [Test]
    public async Task LlmsTextFileReflectsDynamicCatalogChanges()
    {
        // Create a test product
        var uniqueProductName = "AI Test Widget " + Guid.NewGuid().ToString("N")[..8];
        var testProduct = new Product
        {
            Name = uniqueProductName,
            ShortDescription = "A special widget for AI testing.",
            Published = true,
            VisibleIndividually = true,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        await _productRepository.InsertAsync(testProduct);

        try
        {
            // Clear cache so dynamic generation runs
            await _staticCacheManager.RemoveByPrefixAsync(NopModelCacheDefaults.LlmsTextPrefixCacheKey);

            var content = await _llmsModelFactory.PrepareLlmsTextFileAsync(full: false);

            content.Should().Contain(uniqueProductName);
            content.Should().Contain("A special widget for AI testing.");
        }
        finally
        {
            await _productRepository.DeleteAsync(testProduct);
            await _staticCacheManager.RemoveByPrefixAsync(NopModelCacheDefaults.LlmsTextPrefixCacheKey);
        }
    }

    [Test]
    public async Task LlmsTextFileUsesCustomOverrideIfPresent()
    {
        var fileProvider = GetService<INopFileProvider>();
        var customPath = fileProvider.Combine(fileProvider.MapPath("~/wwwroot"), "llms.custom.txt");
        var customContent = "# Custom Store\n\n> This is a custom override test content exceeding 50 characters.\n\n- [Test](https://example.com/test): Test link";

        await fileProvider.WriteAllTextAsync(customPath, customContent, System.Text.Encoding.UTF8);

        try
        {
            var content = await _llmsModelFactory.PrepareLlmsTextFileAsync(full: false);
            content.Should().Be(customContent);
        }
        finally
        {
            if (fileProvider.FileExists(customPath))
                fileProvider.DeleteFile(customPath);
        }
    }
}
