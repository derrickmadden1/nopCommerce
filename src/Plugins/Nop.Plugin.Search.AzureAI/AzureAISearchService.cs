using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

namespace Nop.Plugin.Search.AzureAI;

public class AzureAISearchService
{
    private readonly AzureAISearchSettings _settings;
    private readonly ILogger<AzureAISearchService> _logger;
    private SearchClient? _searchClient;

    public AzureAISearchService(
        AzureAISearchSettings settings,
        ILogger<AzureAISearchService> logger)
    {
        _settings = settings;
        _logger = logger;
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
