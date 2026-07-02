using Nop.Core.Configuration;

namespace Nop.Plugin.Marketing.WinbackEmail;

public class WinbackEmailSettings : ISettings
{
    /// <summary>
    /// Whether the winback flow is active
    /// </summary>
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
    /// Azure OpenAI deployment name e.g. gpt-4o-mini
    /// </summary>
    public string DeploymentName { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Your store name — used in email copy
    /// </summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>
    /// Days since last order before email 1 is sent
    /// </summary>
    public int Email1DaysLapsed { get; set; } = 60;

    /// <summary>
    /// Days since last order before email 2 is sent
    /// </summary>
    public int Email2DaysLapsed { get; set; } = 67;

    /// <summary>
    /// Days since last order before email 3 is sent
    /// </summary>
    public int Email3DaysLapsed { get; set; } = 74;

    /// <summary>
    /// Optional discount code to include in email 3
    /// </summary>
    public string Email3DiscountCode { get; set; } = string.Empty;

    /// <summary>
    /// Email address to send from — must match a nopCommerce email account
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the from address
    /// </summary>
    public string FromName { get; set; } = string.Empty;
}
