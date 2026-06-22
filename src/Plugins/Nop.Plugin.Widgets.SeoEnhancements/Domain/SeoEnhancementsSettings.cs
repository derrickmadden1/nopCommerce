using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.SeoEnhancements.Domain;

public class SeoEnhancementsSettings : ISettings
{
    /// <summary>e.g. https://your-resource.openai.azure.com/</summary>
    public string AzureOpenAiEndpoint { get; set; } = string.Empty;

    /// <summary>Stored encrypted via ISettingService where possible.</summary>
    public string AzureOpenAiApiKey { get; set; } = string.Empty;

    /// <summary>Deployment name, not the base model name (e.g. "gpt-4o-faq").</summary>
    public string AzureOpenAiDeploymentName { get; set; } = string.Empty;

    public string AzureOpenAiApiVersion { get; set; } = "2024-08-01-preview";

    public int FaqPairsToGenerate { get; set; } = 5;

    public bool UseAzureKeyVault { get; set; } = false;

    public string AzureKeyVaultUrl { get; set; } = string.Empty;

    public string AzureKeyVaultSecretName { get; set; } = string.Empty;
}
