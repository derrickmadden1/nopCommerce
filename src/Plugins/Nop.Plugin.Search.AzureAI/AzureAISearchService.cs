using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Search.AzureAI.Messages;
using Nop.Services.Catalog;

namespace Nop.Plugin.Search.AzureAI;

public class AzureAISearchService
{
    private readonly AzureAISearchSettings _settings;
    private readonly ILogger<AzureAISearchService> _logger;
    private readonly ICategoryService _categoryService;
    private readonly IManufacturerService _manufacturerService;
    private SearchClient? _searchClient;

    public AzureAISearchService(
        AzureAISearchSettings settings,
        ILogger<AzureAISearchService> logger,
        ICategoryService categoryService,
        IManufacturerService manufacturerService)
    {
        _settings = settings;
        _logger = logger;
        _categoryService = categoryService;
        _manufacturerService = manufacturerService;
    }

    /// <summary>
    /// Search the Azure AI Search index and return matching nopCommerce product IDs
    /// </summary>
    public async Task<(List<int> ProductIds, long TotalCount)> SearchAsync(
        string query,
        int pageIndex,
        int pageSize,
        decimal? priceMin = null,
        decimal? priceMax = null,
        string? categoryName = null,
        string? manufacturerName = null,
        string? orderBy = null)
    {
        try
        {
            var client = GetSearchClient();

            var filters = BuildFilterString(priceMin, priceMax, categoryName, manufacturerName);

            var options = new SearchOptions
            {
                IncludeTotalCount = true,
                Filter = filters,
                Skip = pageIndex * pageSize,
                Size = pageSize,
                Select = { "id" },
                OrderBy = { MapOrderBy(orderBy) }
            };

            // Use semantic search if query is meaningful text
            // Fall back to wildcard for empty/browse queries
            var searchText = string.IsNullOrWhiteSpace(query) ? "*" : query;

            var response = await client.SearchAsync<SearchDocument>(searchText, options);

            var ids = new List<int>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                if (int.TryParse(result.Document["id"]?.ToString(), out var id))
                    ids.Add(id);
            }

            var total = response.Value.TotalCount ?? 0;
            return (ids, total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure AI Search query failed for query: {Query}", query);
            return (new List<int>(), 0);
        }
    }

    private SearchClient GetSearchClient()
    {
        // Rebuild client if settings have changed
        if (_searchClient == null)
        {
            if (string.IsNullOrWhiteSpace(_settings.Endpoint) || string.IsNullOrWhiteSpace(_settings.QueryApiKey))
                throw new InvalidOperationException("Azure AI Search endpoint and query key must be configured.");

            _searchClient = new SearchClient(
                new Uri(_settings.Endpoint),
                _settings.IndexName,
                new AzureKeyCredential(_settings.QueryApiKey)
            );
        }

        return _searchClient;
    }

    public void ResetClient()
    {
        // Called when settings are saved so client is rebuilt with new config
        _searchClient = null;
    }

    private static string? BuildFilterString(
        decimal? priceMin,
        decimal? priceMax,
        string? categoryName,
        string? manufacturerName)
    {
        var filters = new List<string> { "published eq true" };

        if (priceMin.HasValue)
            filters.Add($"price ge {priceMin.Value}");

        if (priceMax.HasValue)
            filters.Add($"price le {priceMax.Value}");

        if (!string.IsNullOrWhiteSpace(categoryName))
            filters.Add($"categoryNames/any(c: c eq '{EscapeOData(categoryName)}')");

        if (!string.IsNullOrWhiteSpace(manufacturerName))
            filters.Add($"manufacturerNames/any(m: m eq '{EscapeOData(manufacturerName)}')");

        return string.Join(" and ", filters);
    }

    /// <summary>
    /// Gets all product IDs currently in the Azure AI Search index
    /// </summary>
    public async Task<List<int>> GetAllIndexedIdsAsync()
    {
        try
        {
            var client = GetSearchClient();
            var options = new SearchOptions
            {
                Select = { "id" },
                Size = 1000 // Optimized for smaller catalogs; use pagination if > 1000
            };

            var response = await client.SearchAsync<SearchDocument>("*", options);
            var ids = new List<int>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                if (int.TryParse(result.Document["id"]?.ToString(), out var id))
                    ids.Add(id);
            }

            return ids;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve all indexed IDs from Azure AI Search.");
            return new List<int>();
        }
    }

    /// <summary>
    /// Prepares a ProductIndexMessage from a nopCommerce Product entity.
    /// Used by both real-time event consumers and the manual sync tool.
    /// </summary>
    public async Task<ProductIndexMessage> PrepareProductIndexMessageAsync(Product product, ProductIndexAction action)
    {
        var message = new ProductIndexMessage
        {
            ProductId = product.Id,
            Action = action,
            OccurredAtUtc = DateTime.UtcNow
        };

        if (action == ProductIndexAction.Delete)
            return message;

        // Load category names
        var productCategories = await _categoryService.GetProductCategoriesByProductIdAsync(product.Id);
        foreach (var pc in productCategories)
        {
            var category = await _categoryService.GetCategoryByIdAsync(pc.CategoryId);
            if (category != null && !category.Deleted)
                message.CategoryNames.Add(category.Name);
        }

        // Load manufacturer names
        var productManufacturers = await _manufacturerService.GetProductManufacturersByProductIdAsync(product.Id);
        foreach (var pm in productManufacturers)
        {
            var manufacturer = await _manufacturerService.GetManufacturerByIdAsync(pm.ManufacturerId);
            if (manufacturer != null && !manufacturer.Deleted)
                message.ManufacturerNames.Add(manufacturer.Name);
        }

        // Map base product fields
        message.Name = product.Name;
        message.ShortDescription = product.ShortDescription;
        message.FullDescription = product.FullDescription;
        message.Sku = product.Sku;
        message.Price = product.Price;
        message.Published = product.Published;

        return message;
    }

    private static string MapOrderBy(string? orderBy) => orderBy switch
    {
        "price_asc" => "price asc",
        "price_desc" => "price desc",
        "name_asc" => "name asc",
        "name_desc" => "name desc",
        _ => "search.score() desc" // default: relevance
    };

    private static string EscapeOData(string value)
        => value.Replace("'", "''");
}
