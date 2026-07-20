using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Widgets.AiChatbot.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.AzureOpenAIEndpoint")]
    public string? AzureOpenAIEndpoint { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.AzureOpenAIApiKey")]
    public string? AzureOpenAIApiKey { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.DeploymentName")]
    public string? DeploymentName { get; set; } = "gpt-4o-mini";

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.UseAzureKeyVault")]
    public bool UseAzureKeyVault { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.AzureKeyVaultUrl")]
    public string? AzureKeyVaultUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.AzureKeyVaultSecretName")]
    public string? AzureKeyVaultSecretName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.AzureSearchEndpoint")]
    public string? AzureSearchEndpoint { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.AzureSearchQueryKey")]
    public string? AzureSearchQueryKey { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.AzureSearchIndexName")]
    public string? AzureSearchIndexName { get; set; } = "products";

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.BotName")]
    public string? BotName { get; set; } = "Store Assistant";

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.StoreName")]
    public string? StoreName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.WelcomeMessage")]
    public string? WelcomeMessage { get; set; } = "Hi! How can I help you today?";

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.BubbleColour")]
    public string? BubbleColour { get; set; } = "#4A90D9";

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.ReturnsPolicy")]
    public string? ReturnsPolicy { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.ShippingPolicy")]
    public string? ShippingPolicy { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.MaxConversationTurns")]
    public int MaxConversationTurns { get; set; } = 10;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.MaxSearchResults")]
    public int MaxSearchResults { get; set; } = 3;

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.MaxTokens")]
    public int? MaxTokens { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.AiChatbot.Temperature")]
    public float? Temperature { get; set; }
}
