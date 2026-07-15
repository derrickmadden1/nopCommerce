using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

namespace Nop.Plugin.Widgets.AiChatbot.Services;

/// <summary>
/// Searches the Azure AI Search product index to find relevant products
/// for product-related questions. Reuses the existing index from Nop.Plugin.Search.AzureAI.
/// </summary>
public class ProductSearchService
{
    private readonly AiChatbotSettings _settings;
    private readonly ILogger<ProductSearchService> _logger;

    public ProductSearchService(
        AiChatbotSettings settings,
        ILogger<ProductSearchService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Searches for products relevant to the customer's query.
    /// Returns a formatted string for injection into the system prompt.
    /// </summary>
    public async Task<string> GetRelevantProductsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(_settings.AzureSearchEndpoint) ||
            string.IsNullOrWhiteSpace(_settings.AzureSearchQueryKey))
            return string.Empty;

        try
        {
            var client = new SearchClient(
                new Uri(_settings.AzureSearchEndpoint),
                _settings.AzureSearchIndexName,
                new AzureKeyCredential(_settings.AzureSearchQueryKey)
            );

            var options = new SearchOptions
            {
                Size = _settings.MaxSearchResults,
                Select = { "name", "shortDescription", "price", "categoryNames" },
                Filter = "published eq true"
            };

            var response = await client.SearchAsync<SearchDocument>(query, options);
            var results = new List<string>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var name = result.Document["name"]?.ToString();
                var desc = result.Document["shortDescription"]?.ToString();
                var price = result.Document["price"]?.ToString();

                if (name != null)
                    results.Add($"- {name} (£{price}){(desc != null ? $": {desc}" : "")}");
            }

            return results.Any()
                ? string.Join("\n", results)
                : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product search failed for query: {Query}", query);
            return string.Empty;
        }
    }
}
