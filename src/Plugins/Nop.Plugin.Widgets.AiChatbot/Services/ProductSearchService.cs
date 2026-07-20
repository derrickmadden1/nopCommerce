using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

namespace Nop.Plugin.Widgets.AiChatbot.Services;

public class ProductSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string Price { get; set; } = string.Empty;
    public string? Url { get; set; }
}

/// <summary>
/// Searches the Azure AI Search product index for chatbot context.
/// Returns structured results including product IDs so the AI can
/// reference them in add-to-cart and navigation actions.
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
    public async Task<List<ProductSearchResult>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(_settings.AzureSearchEndpoint) ||
            string.IsNullOrWhiteSpace(_settings.AzureSearchQueryKey))
            return new List<ProductSearchResult>();

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
                Select = { "id", "name", "shortDescription", "price", "categoryNames" },
                Filter = "published eq true"
            };

            var response = await client.SearchAsync<SearchDocument>(query, options);
            var results = new List<ProductSearchResult>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var id = result.Document["id"]?.ToString();
                var name = result.Document["name"]?.ToString();
                var desc = result.Document["shortDescription"]?.ToString();
                var price = result.Document["price"]?.ToString();

                if (id != null && name != null && int.TryParse(id, out var productId))
                {
                    results.Add(new ProductSearchResult
                    {
                        Id = productId,
                        Name = name,
                        ShortDescription = desc,
                        Price = price != null ? $"£{decimal.Parse(price):F2}" : string.Empty
                    });
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product search failed for query: {Query}", query);
            return new List<ProductSearchResult>();
        }
    }

    /// <summary>
    /// Formats search results as a string for injection into the system prompt.
    /// Includes product IDs so the AI can reference them in actions.
    /// </summary>
    public static string FormatForPrompt(List<ProductSearchResult> results)
    {
        if (!results.Any())
            return string.Empty;

        return string.Join("\n", results.Select(r =>
            $"- [ID:{r.Id}] {r.Name} ({r.Price})" +
            (r.ShortDescription != null ? $": {r.ShortDescription}" : "")));
    }
}
