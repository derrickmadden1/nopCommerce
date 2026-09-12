using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Widgets.AgentSearch.Models;

namespace Nop.Plugin.Widgets.AgentSearch.Services
{
    /// <summary>
    /// Thin abstraction over the Azure AI Search index that NopSearchIndexer already
    /// keeps in sync. Keeping this as an interface means the API layer below doesn't
    /// care whether you're hitting semantic search, a plain vector query, or a hybrid
    /// of the two — swap the implementation without touching the controller/service.
    /// </summary>
    public interface IAzureProductSearchClient
    {
        Task<AzureSearchQueryResult> SearchAsync(
            string query,
            AgentSearchFilters filters,
            int top);
    }

    public class AzureSearchQueryResult
    {
        public List<AgentProductResult> Products { get; set; } = new();
        public int Total { get; set; }
    }
}
