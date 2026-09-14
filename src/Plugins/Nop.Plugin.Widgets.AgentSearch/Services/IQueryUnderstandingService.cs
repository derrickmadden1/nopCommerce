using System.Threading.Tasks;
using Nop.Plugin.Widgets.AgentSearch.Models;

namespace Nop.Plugin.Widgets.AgentSearch.Services
{
    public class ExtractedQuery
    {
        /// <summary>The query with constraint phrases stripped out, better suited to semantic search.</summary>
        public string CleanedQuery { get; set; } = string.Empty;
        public AgentSearchFilters InferredFilters { get; set; } = new();
        /// <summary>Human-readable summary of what was inferred — surfaced back to the agent as query_interpreted.</summary>
        public string Summary { get; set; } = string.Empty;
    }

    public interface IQueryUnderstandingService
    {
        Task<ExtractedQuery> ExtractAsync(string rawQuery);
    }

    /// <summary>
    /// Default no-op implementation — passes the query through unchanged.
    /// Register this until UseQueryUnderstanding is turned on and you've wired
    /// up a real implementation (e.g. calling your Azure OpenAI resource, the
    /// same one the SeoEnhancements plugin already uses for FAQ generation).
    /// </summary>
    public class PassthroughQueryUnderstandingService : IQueryUnderstandingService
    {
        public Task<ExtractedQuery> ExtractAsync(string rawQuery)
        {
            return Task.FromResult(new ExtractedQuery
            {
                CleanedQuery = rawQuery,
                InferredFilters = new AgentSearchFilters(),
                Summary = rawQuery
            });
        }
    }
}
