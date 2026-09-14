using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Nop.Plugin.Widgets.AgentSearch.Models;

namespace Nop.Plugin.Widgets.AgentSearch.Services
{
    /// <summary>
    /// Search client that queries the Azure AI Search index populated by Nop.Plugin.Search.AzureAI.
    /// </summary>
    public class AzureProductSearchClient : IAzureProductSearchClient
    {
        private readonly SearchClient _searchClient;

        public AzureProductSearchClient(AzureSearchServiceOptions options)
        {
            var endpoint = string.IsNullOrWhiteSpace(options.ServiceEndpoint) ? "https://localhost" : options.ServiceEndpoint;
            var indexName = string.IsNullOrWhiteSpace(options.IndexName) ? "products" : options.IndexName;
            var apiKey = string.IsNullOrWhiteSpace(options.ApiKey) ? "placeholder" : options.ApiKey;

            _searchClient = new SearchClient(
                new Uri(endpoint),
                indexName,
                new AzureKeyCredential(apiKey));
        }

        public async Task<AzureSearchQueryResult> SearchAsync(
            string query,
            AgentSearchFilters filters,
            int top)
        {
            var options = new SearchOptions
            {
                Size = top,
                IncludeTotalCount = true,
                QueryType = SearchQueryType.Simple
            };

            var filterClauses = BuildFilterClauses(filters);
            if (filterClauses.Count > 0)
                options.Filter = string.Join(" and ", filterClauses);

            // Select fields matching Nop.Plugin.Search.AzureAI schema
            options.Select.Add("id");
            options.Select.Add("name");
            options.Select.Add("shortDescription");
            options.Select.Add("price");
            options.Select.Add("published");
            options.Select.Add("categoryNames");
            options.Select.Add("manufacturerNames");

            var searchText = string.IsNullOrWhiteSpace(query) ? "*" : query;
            var response = await _searchClient.SearchAsync<SearchDocument>(searchText, options);

            var result = new AzureSearchQueryResult
            {
                Total = (int)(response.Value.TotalCount ?? 0)
            };

            await foreach (var doc in response.Value.GetResultsAsync())
            {
                result.Products.Add(MapDocument(doc.Document));
            }

            return result;
        }

        private static List<string> BuildFilterClauses(AgentSearchFilters filters)
        {
            var clauses = new List<string> { "published eq true" };

            if (filters == null)
                return clauses;

            if (filters.MaxPrice.HasValue)
                clauses.Add($"price le {filters.MaxPrice.Value}");

            if (filters.MinPrice.HasValue)
                clauses.Add($"price ge {filters.MinPrice.Value}");

            if (!string.IsNullOrWhiteSpace(filters.Category))
                clauses.Add($"categoryNames/any(c: c eq '{Escape(filters.Category)}')");

            return clauses;
        }

        private static string Escape(string value) => value.Replace("'", "''");

        private static AgentProductResult MapDocument(SearchDocument doc)
        {
            var categories = new List<string>();
            if (doc.TryGetValue("categoryNames", out var catObj) && catObj is IEnumerable catEnum && !(catObj is string))
            {
                foreach (var cat in catEnum)
                {
                    if (cat != null)
                        categories.Add(cat.ToString());
                }
            }

            var manufacturers = new List<string>();
            if (doc.TryGetValue("manufacturerNames", out var mfgObj) && mfgObj is IEnumerable mfgEnum && !(mfgObj is string))
            {
                foreach (var mfg in mfgEnum)
                {
                    if (mfg != null)
                        manufacturers.Add(mfg.ToString());
                }
            }

            return new AgentProductResult
            {
                Id = doc.TryGetValue("id", out var id) ? id?.ToString() : null,
                Name = doc.TryGetValue("name", out var name) ? name?.ToString() : null,
                Description = doc.TryGetValue("shortDescription", out var desc) ? desc?.ToString() : null,
                Price = new AgentPrice
                {
                    Amount = doc.TryGetValue("price", out var price) && price != null
                        ? Convert.ToDecimal(price)
                        : 0m,
                    Currency = "GBP"
                },
                Availability = "in_stock",
                CategoryNames = categories,
                ManufacturerNames = manufacturers
            };
        }
    }

    public class AzureSearchServiceOptions
    {
        public string ServiceEndpoint { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}
