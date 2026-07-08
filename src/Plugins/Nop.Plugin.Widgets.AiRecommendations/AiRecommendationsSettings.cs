using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.AiRecommendations;

public class AiRecommendationsSettings : ISettings
{
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Azure OpenAI endpoint e.g. https://yourresource.openai.azure.com
    /// </summary>
    public string AzureOpenAIEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI API key
    /// </summary>
    public string AzureOpenAIApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Embedding model deployment name e.g. text-embedding-ada-002
    /// </summary>
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-ada-002";

    /// <summary>
    /// Number of recommendations to show per widget
    /// </summary>
    public int RecommendationCount { get; set; } = 4;

    /// <summary>
    /// Show personalised recommendations on homepage (requires customer to be logged in)
    /// </summary>
    public bool ShowOnHomepage { get; set; } = true;

    /// <summary>
    /// Show similar product recommendations on product pages
    /// </summary>
    public bool ShowOnProductPage { get; set; } = true;

    /// <summary>
    /// Show recommendations on the cart page
    /// </summary>
    public bool ShowOnCart { get; set; } = true;

    /// <summary>
    /// Minimum cosine similarity score to include a recommendation (0.0 - 1.0)
    /// </summary>
    public double MinSimilarityScore { get; set; } = 0.75;

    public bool UseAzureKeyVault { get; set; } = false;

    public string AzureKeyVaultUrl { get; set; } = string.Empty;

    public string AzureKeyVaultSecretName { get; set; } = string.Empty;
}
