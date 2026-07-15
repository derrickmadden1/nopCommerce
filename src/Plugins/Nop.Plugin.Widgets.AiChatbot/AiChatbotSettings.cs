using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.AiChatbot;

public class AiChatbotSettings : ISettings
{
    public bool Enabled { get; set; } = false;

    // Azure OpenAI
    public string AzureOpenAIEndpoint { get; set; } = string.Empty;
    public string AzureOpenAIApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o-mini";

    // Azure AI Search (for product Q&A — reuses existing index)
    public string AzureSearchEndpoint { get; set; } = string.Empty;
    public string AzureSearchQueryKey { get; set; } = string.Empty;
    public string AzureSearchIndexName { get; set; } = "products";

    // Chatbot persona
    public string BotName { get; set; } = "Store Assistant";
    public string StoreName { get; set; } = string.Empty;
    public string WelcomeMessage { get; set; } = "Hi! How can I help you today?";
    public string BubbleColour { get; set; } = "#4A90D9";

    // Store policies — pasted as plain text, injected into system prompt
    public string ReturnsPolicy { get; set; } = string.Empty;
    public string ShippingPolicy { get; set; } = string.Empty;

    // Limits
    public int MaxConversationTurns { get; set; } = 10;
    public int MaxSearchResults { get; set; } = 3;
}
