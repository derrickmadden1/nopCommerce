using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Marketing.WinbackEmail.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.StoreName")]
    public string StoreName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.AzureOpenAIEndpoint")]
    public string AzureOpenAIEndpoint { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.AzureOpenAIApiKey")]
    public string AzureOpenAIApiKey { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.DeploymentName")]
    public string DeploymentName { get; set; } = "gpt-4o-mini";

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.FromEmail")]
    public string FromEmail { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.FromName")]
    public string FromName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.Email1DaysLapsed")]
    public int Email1DaysLapsed { get; set; } = 60;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.Email2DaysLapsed")]
    public int Email2DaysLapsed { get; set; } = 67;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.Email3DaysLapsed")]
    public int Email3DaysLapsed { get; set; } = 74;

    [NopResourceDisplayName("Plugins.Marketing.WinbackEmail.Email3DiscountCode")]
    public string Email3DiscountCode { get; set; } = string.Empty;
}
