using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.AiChatbot.Models;
using Nop.Plugin.Widgets.AiChatbot.Services;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.AiChatbot.Controllers;

public class AiChatbotController : BasePluginController
{
    private readonly AiChatbotSettings _settings;
    private readonly ChatService _chatService;
    private readonly ISettingService _settingService;
    private readonly INotificationService _notificationService;

    public AiChatbotController(
        AiChatbotSettings settings,
        ChatService chatService,
        ISettingService settingService,
        INotificationService notificationService)
    {
        _settings = settings;
        _chatService = chatService;
        _settingService = settingService;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Chat endpoint — called by the frontend widget via fetch()
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Route("AiChatbot/Chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (!_settings.Enabled)
            return Json(new ChatResponse { Success = false, Error = "Chat is not available." });

        if (string.IsNullOrWhiteSpace(request.Message))
            return Json(new ChatResponse { Success = false, Error = "Please enter a message." });

        // Basic input sanitation
        request.Message = request.Message.Trim()[..Math.Min(request.Message.Trim().Length, 500)];

        var response = await _chatService.GetResponseAsync(request);
        return Json(response);
    }

    // ── Admin ─────────────────────────────────────────────────────────────────

    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [AutoValidateAntiforgeryToken]
    public IActionResult Configure()
    {
        var model = new ConfigurationModel
        {
            Enabled = _settings.Enabled,
            AzureOpenAIEndpoint = _settings.AzureOpenAIEndpoint,
            AzureOpenAIApiKey = _settings.AzureOpenAIApiKey,
            DeploymentName = _settings.DeploymentName,
            UseAzureKeyVault = _settings.UseAzureKeyVault,
            AzureKeyVaultUrl = _settings.AzureKeyVaultUrl,
            AzureKeyVaultSecretName = _settings.AzureKeyVaultSecretName,
            AzureSearchEndpoint = _settings.AzureSearchEndpoint,
            AzureSearchQueryKey = _settings.AzureSearchQueryKey,
            AzureSearchIndexName = _settings.AzureSearchIndexName,
            BotName = _settings.BotName,
            StoreName = _settings.StoreName,
            WelcomeMessage = _settings.WelcomeMessage,
            BubbleColour = _settings.BubbleColour,
            ReturnsPolicy = _settings.ReturnsPolicy,
            ShippingPolicy = _settings.ShippingPolicy,
            MaxConversationTurns = _settings.MaxConversationTurns,
            MaxSearchResults = _settings.MaxSearchResults,
            MaxTokens = _settings.MaxTokens,
            Temperature = _settings.Temperature
        };

        return View("~/Plugins/Widgets.AiChatbot/Views/Configure.cshtml", model);
    }

    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [HttpPost]
    [AutoValidateAntiforgeryToken]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = string.Join("<br/>", ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage));
            _notificationService.ErrorNotification($"Validation failed: {errors}");
            return View("~/Plugins/Widgets.AiChatbot/Views/Configure.cshtml", model);
        }

        _settings.Enabled = model.Enabled;
        _settings.AzureOpenAIEndpoint = model.AzureOpenAIEndpoint?.Trim() ?? string.Empty;
        _settings.AzureOpenAIApiKey = model.AzureOpenAIApiKey?.Trim() ?? string.Empty;
        _settings.DeploymentName = model.DeploymentName?.Trim() ?? string.Empty;
        _settings.UseAzureKeyVault = model.UseAzureKeyVault;
        _settings.AzureKeyVaultUrl = model.AzureKeyVaultUrl?.Trim() ?? string.Empty;
        _settings.AzureKeyVaultSecretName = model.AzureKeyVaultSecretName?.Trim() ?? string.Empty;
        _settings.AzureSearchEndpoint = model.AzureSearchEndpoint?.Trim() ?? string.Empty;
        _settings.AzureSearchQueryKey = model.AzureSearchQueryKey?.Trim() ?? string.Empty;
        _settings.AzureSearchIndexName = model.AzureSearchIndexName?.Trim() ?? string.Empty;
        _settings.BotName = model.BotName?.Trim() ?? string.Empty;
        _settings.StoreName = model.StoreName?.Trim() ?? string.Empty;
        _settings.WelcomeMessage = model.WelcomeMessage?.Trim() ?? string.Empty;
        _settings.BubbleColour = model.BubbleColour?.Trim() ?? string.Empty;
        _settings.ReturnsPolicy = model.ReturnsPolicy ?? string.Empty;
        _settings.ShippingPolicy = model.ShippingPolicy ?? string.Empty;
        _settings.MaxConversationTurns = model.MaxConversationTurns;
        _settings.MaxSearchResults = model.MaxSearchResults;
        _settings.MaxTokens = model.MaxTokens;
        _settings.Temperature = model.Temperature;

        await _settingService.SaveSettingAsync(_settings);
        _notificationService.SuccessNotification("AI Chatbot settings saved.");

        return RedirectToAction("Configure");
    }
}
