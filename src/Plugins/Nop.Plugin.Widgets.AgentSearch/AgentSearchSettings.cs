using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.AgentSearch
{
    public class AgentSearchSettings : ISettings
    {
        /// <summary>Master on/off switch for the endpoint.</summary>
        public bool Enabled { get; set; }

        /// <summary>Results returned when the caller doesn't specify max_results.</summary>
        public int MaxResultsDefault { get; set; }

        /// <summary>Hard ceiling regardless of what the caller asks for.</summary>
        public int MaxResultsCap { get; set; }

        /// <summary>If true, callers must send a valid key (see ApiKey) via X-Api-Key header.</summary>
        public bool RequireApiKey { get; set; }

        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// If true, runs the natural-language query through a constraint-extraction
        /// pass (e.g. "under £60", "ships to Scotland") before hitting Azure AI Search,
        /// rather than relying purely on semantic relevance.
        /// </summary>
        public bool UseQueryUnderstanding { get; set; }
    }
}
