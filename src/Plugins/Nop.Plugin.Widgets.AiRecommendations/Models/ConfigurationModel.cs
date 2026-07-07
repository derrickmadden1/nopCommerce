using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Widgets.AiRecommendations.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Widgets.AiRecommendations.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.AiRecommendations.AzureOpenAIEndpoint")]
    public string AzureOpenAIEndpoint { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiRecommendations.AzureOpenAIApiKey")]
    public string AzureOpenAIApiKey { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiRecommendations.EmbeddingDeploymentName")]
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-ada-002";

    [NopResourceDisplayName("Plugins.Widgets.AiRecommendations.RecommendationCount")]
    public int RecommendationCount { get; set; } = 4;

    [NopResourceDisplayName("Plugins.Widgets.AiRecommendations.MinSimilarityScore")]
    public double MinSimilarityScore { get; set; } = 0.75;

    [NopResourceDisplayName("Plugins.Widgets.AiRecommendations.ShowOnHomepage")]
    public bool ShowOnHomepage { get; set; } = true;

    [NopResourceDisplayName("Plugins.Widgets.AiRecommendations.ShowOnProductPage")]
    public bool ShowOnProductPage { get; set; } = true;

    [NopResourceDisplayName("Plugins.Widgets.AiRecommendations.ShowOnCart")]
    public bool ShowOnCart { get; set; } = true;
}
