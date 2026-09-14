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
}
