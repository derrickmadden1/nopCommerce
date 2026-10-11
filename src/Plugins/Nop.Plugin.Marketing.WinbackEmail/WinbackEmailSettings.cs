using Nop.Core.Configuration;

namespace Nop.Plugin.Marketing.WinbackEmail;

public class WinbackEmailSettings : ISettings
{
    /// <summary>
    /// Whether the winback flow is active
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// If true, emails are queued but their send date is set far into the future (year 2099) so they are never actually sent.
    /// This allows you to view the generated emails in the Message Queue without emailing real customers.
    /// </summary>
    public bool DryRun { get; set; } = false;

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

    public bool UseAzureKeyVault { get; set; } = false;

    public string AzureKeyVaultUrl { get; set; } = string.Empty;

    public string AzureKeyVaultSecretName { get; set; } = string.Empty;

    /// <summary>
    /// Your store name — used in email copy
    /// </summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>
    /// Days since last order before email 1 is sent
    /// </summary>
    public int Email1DaysLapsed { get; set; } = 60;

    /// <summary>
    /// Days to wait after Email 1 before sending Email 2
    /// </summary>
    public int DaysBetweenEmail1And2 { get; set; } = 7;

    /// <summary>
    /// Days to wait after Email 2 before sending Email 3
    /// </summary>
    public int DaysBetweenEmail2And3 { get; set; } = 7;

    /// <summary>
    /// Maximum days since last order to consider a customer for winback emails
    /// </summary>
    public int MaxDaysLapsed { get; set; } = 365;

    /// <summary>
    /// Discount amount/percentage to offer in Email 3 (set to 0 to disable dynamic discount generation)
    /// </summary>
    public decimal Email3DiscountAmount { get; set; } = 10m;

    /// <summary>
    /// Whether Email3DiscountAmount is a percentage (true) or fixed amount (false)
    /// </summary>
    public bool Email3DiscountUsePercentage { get; set; } = true;

    /// <summary>
    /// Prefix for auto-generated single-use coupon codes (e.g., "WB-")
    /// </summary>
    public string Email3DiscountPrefix { get; set; } = "WB-";

    /// <summary>
    /// Validity period in days for the generated single-use coupon code (0 = no expiry)
    /// </summary>
    public int Email3DiscountExpiryDays { get; set; } = 14;

    /// <summary>
    /// Optional fallback static discount code to include in email 3 if Email3DiscountAmount is 0
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
