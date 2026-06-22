using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Widgets.SeoEnhancements.Models;

public record SeoEnhancementsConfigModel : BaseNopModel
{
    [NopResourceDisplayName("Azure OpenAI Endpoint")]
    public string AzureOpenAiEndpoint { get; set; } = string.Empty;

    [NopResourceDisplayName("Azure OpenAI API Key")]
    [DataType(DataType.Password)]
    public string AzureOpenAiApiKey { get; set; } = string.Empty;

    [NopResourceDisplayName("Deployment Name")]
    public string AzureOpenAiDeploymentName { get; set; } = string.Empty;

    [NopResourceDisplayName("API Version")]
    public string AzureOpenAiApiVersion { get; set; } = "2024-08-01-preview";

    [NopResourceDisplayName("FAQ pairs to generate per request")]
    [Range(1, 10)]
    public int FaqPairsToGenerate { get; set; } = 5;

    [NopResourceDisplayName("Use Azure Key Vault")]
    public bool UseAzureKeyVault { get; set; }

    [NopResourceDisplayName("Azure Key Vault URL")]
    public string AzureKeyVaultUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Azure Key Vault Secret Name")]
    public string AzureKeyVaultSecretName { get; set; } = string.Empty;
}

/// <summary>One generated pair shown in the review screen, with an Include checkbox.</summary>
public record GeneratedFaqReviewItem
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public bool Include { get; set; } = true;
}

public record FaqGenerateRequestModel
{
    public int EntityTypeId { get; set; }
    public int EntityId { get; set; }
}

public record FaqGenerateReviewModel
{
    public int EntityTypeId { get; set; }
    public int EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public List<GeneratedFaqReviewItem> Candidates { get; set; } = new();
}
