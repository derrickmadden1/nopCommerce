using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.AiChatbot.Components;

/// <summary>
/// Injects the chat bubble into every page via body_end_html_tag_before widget zone
/// </summary>
public class ChatWidgetViewComponent : NopViewComponent
{
    private readonly AiChatbotSettings _settings;

    public ChatWidgetViewComponent(AiChatbotSettings settings)
    {
        _settings = settings;
    }

    public IViewComponentResult Invoke(string widgetZone, object? additionalData = null)
    {
        if (!_settings.Enabled)
            return Content(string.Empty);

        return View("~/Plugins/Widgets.AiChatbot/Views/ChatWidget.cshtml", _settings);
    }
}
