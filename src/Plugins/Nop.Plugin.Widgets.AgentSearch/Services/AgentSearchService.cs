using System.Threading.Tasks;
using Nop.Plugin.Widgets.AgentSearch.Models;

namespace Nop.Plugin.Widgets.AgentSearch.Services
{
    public interface IAgentSearchService
    {
        Task<AgentSearchResponse> SearchAsync(AgentSearchRequest request, AgentSearchSettings settings);
    }

    public class AgentSearchService : IAgentSearchService
    {
        private readonly IAzureProductSearchClient _searchClient;
        private readonly IQueryUnderstandingService _queryUnderstanding;

        public AgentSearchService(
            IAzureProductSearchClient searchClient,
            IQueryUnderstandingService queryUnderstanding)
        {
            _searchClient = searchClient;
            _queryUnderstanding = queryUnderstanding;
        }

        public async Task<AgentSearchResponse> SearchAsync(AgentSearchRequest request, AgentSearchSettings settings)
        {
            var top = request.MaxResults ?? settings.MaxResultsDefault;
            if (top > settings.MaxResultsCap)
                top = settings.MaxResultsCap;

            var filters = request.Filters ?? new AgentSearchFilters();
            var queryText = request.Query ?? string.Empty;
            var interpretedSummary = queryText;

            if (settings.UseQueryUnderstanding)
            {
                var extracted = await _queryUnderstanding.ExtractAsync(queryText);
                queryText = extracted.CleanedQuery;
                filters = MergeFilters(filters, extracted.InferredFilters);
                interpretedSummary = extracted.Summary;
            }

            var result = await _searchClient.SearchAsync(queryText, filters, top);

            return new AgentSearchResponse
            {
                Results = result.Products,
                Total = result.Total,
                QueryInterpreted = interpretedSummary
            };
        }

        // Explicit filters on the request always win over anything the LLM inferred.
        private static AgentSearchFilters MergeFilters(AgentSearchFilters explicitFilters, AgentSearchFilters inferred)
        {
            if (inferred == null)
                return explicitFilters;

            return new AgentSearchFilters
            {
                InStock = explicitFilters.InStock ?? inferred.InStock,
                MaxPrice = explicitFilters.MaxPrice ?? inferred.MaxPrice,
                MinPrice = explicitFilters.MinPrice ?? inferred.MinPrice,
                ShipsTo = explicitFilters.ShipsTo ?? inferred.ShipsTo,
                Category = explicitFilters.Category ?? inferred.Category
            };
        }
    }
}
