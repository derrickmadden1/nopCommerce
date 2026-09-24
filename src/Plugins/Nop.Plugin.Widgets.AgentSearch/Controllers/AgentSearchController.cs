using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.AgentSearch.Models;
using Nop.Plugin.Widgets.AgentSearch.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.AgentSearch.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class AgentSearchController : BasePluginController
{
    private readonly AgentSearchSettings _settings;
    private readonly ISettingService _settingService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IAgentKeyService _agentKeyService;

    public AgentSearchController(
        AgentSearchSettings settings,
        ISettingService settingService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        IAgentKeyService agentKeyService)
    {
        _settings = settings;
        _settingService = settingService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _agentKeyService = agentKeyService;
    }

    public async Task<IActionResult> Configure()
    {
        var keys = await _agentKeyService.GetAllKeysAsync();

        var model = new AgentSearchConfigurationModel
        {
            Enabled = _settings.Enabled,
            MaxResultsDefault = _settings.MaxResultsDefault,
            MaxResultsCap = _settings.MaxResultsCap,
            RequireApiKey = _settings.RequireApiKey,
            ApiKey = _settings.ApiKey,
            UseQueryUnderstanding = _settings.UseQueryUnderstanding,
            ExistingKeys = keys
        };

        return View("~/Plugins/Widgets.AgentSearch/Views/Configure.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(AgentSearchConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        _settings.Enabled = model.Enabled;
        _settings.MaxResultsDefault = model.MaxResultsDefault;
        _settings.MaxResultsCap = model.MaxResultsCap;
        _settings.RequireApiKey = model.RequireApiKey;
        _settings.ApiKey = model.ApiKey ?? string.Empty;
        _settings.UseQueryUnderstanding = model.UseQueryUnderstanding;

        await _settingService.SaveSettingAsync(_settings);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    [HttpPost]
    public async Task<IActionResult> CreateKey(AgentSearchConfigurationModel model)
    {
        if (string.IsNullOrWhiteSpace(model.NewAgentName))
        {
            _notificationService.ErrorNotification("Agent Name is required when generating a new API Key.");
            return await Configure();
        }

        var (keyEntity, rawKey) = await _agentKeyService.CreateKeyAsync(
            model.NewAgentName,
            model.NewRateLimitPerMinute > 0 ? model.NewRateLimitPerMinute : 600,
            model.NewAllowedScopes);

        _notificationService.SuccessNotification($"Agent API Key generated successfully for '{keyEntity.AgentName}'! RAW SECRET KEY (copy now, it will not be shown again): {rawKey}");

        return await Configure();
    }

    [HttpPost]
    public async Task<IActionResult> ToggleKeyStatus(int id)
    {
        await _agentKeyService.ToggleKeyStatusAsync(id);
        _notificationService.SuccessNotification("Agent API Key status toggled successfully.");
        return await Configure();
    }

    [HttpPost]
    public async Task<IActionResult> DeleteKey(int id)
    {
        await _agentKeyService.DeleteKeyAsync(id);
        _notificationService.SuccessNotification("Agent API Key deleted successfully.");
        return await Configure();
    }
}
