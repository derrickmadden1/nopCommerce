#nullable disable

using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Marketing.WinbackEmail.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.DryRun")]
    public bool DryRun { get; set; }

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.StoreName")]
    public string StoreName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.AzureOpenAIEndpoint")]
    public string AzureOpenAIEndpoint { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.AzureOpenAIApiKey")]
    public string AzureOpenAIApiKey { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.DeploymentName")]
    public string DeploymentName { get; set; } = "gpt-4o-mini";

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.UseAzureKeyVault")]
    public bool UseAzureKeyVault { get; set; }

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.AzureKeyVaultUrl")]
    public string AzureKeyVaultUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.AzureKeyVaultSecretName")]
    public string AzureKeyVaultSecretName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.FromEmail")]
    public string FromEmail { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.FromName")]
    public string FromName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.Email1DaysLapsed")]
    public int Email1DaysLapsed { get; set; } = 60;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.DaysBetweenEmail1And2")]
    public int DaysBetweenEmail1And2 { get; set; } = 7;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.DaysBetweenEmail2And3")]
    public int DaysBetweenEmail2And3 { get; set; } = 7;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.MaxDaysLapsed")]
    public int MaxDaysLapsed { get; set; } = 365;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.Email3DiscountCode")]
    public string Email3DiscountCode { get; set; } = string.Empty;
}
