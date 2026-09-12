using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.AgentSearch.Models;
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

    public AgentSearchController(
        AgentSearchSettings settings,
        ISettingService settingService,
        INotificationService notificationService,
        ILocalizationService localizationService)
    {
        _settings = settings;
        _settingService = settingService;
        _notificationService = notificationService;
        _localizationService = localizationService;
    }

    public IActionResult Configure()
    {
        var model = new AgentSearchConfigurationModel
        {
            Enabled = _settings.Enabled,
            MaxResultsDefault = _settings.MaxResultsDefault,
            MaxResultsCap = _settings.MaxResultsCap,
            RequireApiKey = _settings.RequireApiKey,
            ApiKey = _settings.ApiKey,
            UseQueryUnderstanding = _settings.UseQueryUnderstanding
        };

        return View("~/Plugins/Widgets.AgentSearch/Views/Configure.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(AgentSearchConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return Configure();

        _settings.Enabled = model.Enabled;
        _settings.MaxResultsDefault = model.MaxResultsDefault;
        _settings.MaxResultsCap = model.MaxResultsCap;
        _settings.RequireApiKey = model.RequireApiKey;
        _settings.ApiKey = model.ApiKey ?? string.Empty;
        _settings.UseQueryUnderstanding = model.UseQueryUnderstanding;

        await _settingService.SaveSettingAsync(_settings);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return Configure();
    }
}
