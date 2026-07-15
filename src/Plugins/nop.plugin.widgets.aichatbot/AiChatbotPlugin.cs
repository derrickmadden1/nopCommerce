using Nop.Core;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.AiChatbot;

public class AiChatbotPlugin : BasePlugin, IWidgetPlugin
{
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;
    private readonly ILocalizationService _localizationService;

    public bool HideInWidgetList => false;

    public AiChatbotPlugin(
        ISettingService settingService,
        IWebHelper webHelper,
        ILocalizationService localizationService)
    {
        _settingService = settingService;
        _webHelper = webHelper;
        _localizationService = localizationService;
    }

    public Task<IList<string>> GetWidgetZonesAsync()
    {
        // Inject just before </body> on every page
        return Task.FromResult<IList<string>>(new List<string>
        {
            PublicWidgetZones.BodyEndHtmlTagBefore
        });
    }

    public Type? GetWidgetViewComponent(string widgetZone)
        => typeof(Components.ChatWidgetViewComponent);

    public override string GetConfigurationPageUrl()
        => $"{_webHelper.GetStoreLocation()}Admin/AiChatbot/Configure";

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new AiChatbotSettings
        {
            Enabled = false,
            DeploymentName = "gpt-4o-mini",
            AzureSearchIndexName = "products",
            BotName = "Store Assistant",
            WelcomeMessage = "Hi! How can I help you today?",
            BubbleColour = "#4A90D9",
            MaxConversationTurns = 10,
            MaxSearchResults = 3
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Widgets.AiChatbot.Enabled"] = "Enable AI Chatbot",
            ["Plugins.Widgets.AiChatbot.AzureOpenAIEndpoint"] = "Azure OpenAI Endpoint",
            ["Plugins.Widgets.AiChatbot.AzureOpenAIApiKey"] = "Azure OpenAI API Key",
            ["Plugins.Widgets.AiChatbot.DeploymentName"] = "Deployment Name",
            ["Plugins.Widgets.AiChatbot.AzureSearchEndpoint"] = "Azure AI Search Endpoint",
            ["Plugins.Widgets.AiChatbot.AzureSearchQueryKey"] = "Azure AI Search Query Key",
            ["Plugins.Widgets.AiChatbot.AzureSearchIndexName"] = "Azure AI Search Index Name",
            ["Plugins.Widgets.AiChatbot.BotName"] = "Bot Name",
            ["Plugins.Widgets.AiChatbot.StoreName"] = "Store Name",
            ["Plugins.Widgets.AiChatbot.WelcomeMessage"] = "Welcome Message",
            ["Plugins.Widgets.AiChatbot.BubbleColour"] = "Chat Bubble Colour",
            ["Plugins.Widgets.AiChatbot.ReturnsPolicy"] = "Returns Policy",
            ["Plugins.Widgets.AiChatbot.ShippingPolicy"] = "Shipping Policy",
            ["Plugins.Widgets.AiChatbot.MaxConversationTurns"] = "Max Conversation Turns"
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<AiChatbotSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Widgets.AiChatbot");
        await base.UninstallAsync();
    }
}
