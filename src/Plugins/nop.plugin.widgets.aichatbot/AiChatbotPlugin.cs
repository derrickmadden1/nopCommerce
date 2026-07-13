using Nop.Core;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.AiChatbot;

public class AiChatbotPlugin : BasePlugin, IWidgetPlugin
{
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;

    public bool HideInWidgetList => false;

    public AiChatbotPlugin(
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _settingService = settingService;
        _webHelper = webHelper;
    }

    public IList<string> GetWidgetZones()
    {
        // Inject just before </body> on every page
        return new List<string>
        {
            PublicWidgetZones.BodyEndHtmlTagBefore
        };
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

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<AiChatbotSettings>();
        await base.UninstallAsync();
    }
}
