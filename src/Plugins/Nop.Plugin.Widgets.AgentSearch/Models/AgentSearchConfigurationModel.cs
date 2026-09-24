using System.Collections.Generic;
using Nop.Plugin.Widgets.AgentSearch.Domain;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Widgets.AgentSearch.Models;

public record AgentSearchConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Widgets.AgentSearch.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.AgentSearch.MaxResultsDefault")]
    public int MaxResultsDefault { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.AgentSearch.MaxResultsCap")]
    public int MaxResultsCap { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.AgentSearch.RequireApiKey")]
    public bool RequireApiKey { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.AgentSearch.ApiKey")]
    public string? ApiKey { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.AgentSearch.UseQueryUnderstanding")]
    public bool UseQueryUnderstanding { get; set; }

    /// <summary>
    /// List of registered multi-tenant Agent API keys.
    /// </summary>
    public IList<AgentApiKey> ExistingKeys { get; set; } = new List<AgentApiKey>();

    /// <summary>
    /// Model fields for generating a new key.
    /// </summary>
    public string NewAgentName { get; set; } = string.Empty;
    public int NewRateLimitPerMinute { get; set; } = 600;
    public string NewAllowedScopes { get; set; } = "Search.Read,Catalog.Read";
}
