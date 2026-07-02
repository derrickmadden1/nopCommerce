using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Marketing.WinbackEmail.Models;
using Nop.Plugin.Marketing.WinbackEmail.Services;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Marketing.WinbackEmail.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class WinbackEmailController : BasePluginController
{
    private readonly WinbackEmailSettings _settings;
    private readonly ISettingService _settingService;
    private readonly INotificationService _notificationService;
    private readonly WinbackEmailService _winbackEmailService;

    public WinbackEmailController(
        WinbackEmailSettings settings,
        ISettingService settingService,
        INotificationService notificationService,
        WinbackEmailService winbackEmailService)
    {
        _settings = settings;
        _settingService = settingService;
        _notificationService = notificationService;
        _winbackEmailService = winbackEmailService;
    }

    public IActionResult Configure()
    {
        var model = new ConfigurationModel
        {
            Enabled = _settings.Enabled,
            StoreName = _settings.StoreName,
            AzureOpenAIEndpoint = _settings.AzureOpenAIEndpoint,
            AzureOpenAIApiKey = _settings.AzureOpenAIApiKey,
            DeploymentName = _settings.DeploymentName,
            FromEmail = _settings.FromEmail,
            FromName = _settings.FromName,
            Email1DaysLapsed = _settings.Email1DaysLapsed,
            Email2DaysLapsed = _settings.Email2DaysLapsed,
            Email3DaysLapsed = _settings.Email3DaysLapsed,
            Email3DiscountCode = _settings.Email3DiscountCode
        };

        return View("~/Plugins/Marketing.WinbackEmail/Views/Configure.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Plugins/Marketing.WinbackEmail/Views/Configure.cshtml", model);

        _settings.Enabled = model.Enabled;
        _settings.StoreName = model.StoreName;
        _settings.AzureOpenAIEndpoint = model.AzureOpenAIEndpoint.Trim();
        _settings.AzureOpenAIApiKey = model.AzureOpenAIApiKey.Trim();
        _settings.DeploymentName = model.DeploymentName.Trim();
        _settings.FromEmail = model.FromEmail.Trim();
        _settings.FromName = model.FromName.Trim();
        _settings.Email1DaysLapsed = model.Email1DaysLapsed;
        _settings.Email2DaysLapsed = model.Email2DaysLapsed;
        _settings.Email3DaysLapsed = model.Email3DaysLapsed;
        _settings.Email3DiscountCode = model.Email3DiscountCode.Trim();

        await _settingService.SaveSettingAsync(_settings);
        _notificationService.SuccessNotification("Winback email settings saved.");

        return RedirectToAction("Configure");
    }

    /// <summary>
    /// Manually trigger the winback task from the admin UI for testing
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RunNow()
    {
        await _winbackEmailService.ProcessWinbacksAsync();
        _notificationService.SuccessNotification("Winback task executed — check the email queue for results.");
        return RedirectToAction("Configure");
    }
}
