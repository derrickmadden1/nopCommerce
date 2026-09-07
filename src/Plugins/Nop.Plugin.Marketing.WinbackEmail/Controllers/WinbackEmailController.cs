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
            DryRun = _settings.DryRun,
            StoreName = _settings.StoreName,
            AzureOpenAIEndpoint = _settings.AzureOpenAIEndpoint,
            AzureOpenAIApiKey = _settings.AzureOpenAIApiKey,
            DeploymentName = _settings.DeploymentName,
            UseAzureKeyVault = _settings.UseAzureKeyVault,
            AzureKeyVaultUrl = _settings.AzureKeyVaultUrl,
            AzureKeyVaultSecretName = _settings.AzureKeyVaultSecretName,
            FromEmail = _settings.FromEmail,
            FromName = _settings.FromName,
            Email1DaysLapsed = _settings.Email1DaysLapsed,
            DaysBetweenEmail1And2 = _settings.DaysBetweenEmail1And2,
            DaysBetweenEmail2And3 = _settings.DaysBetweenEmail2And3,
            MaxDaysLapsed = _settings.MaxDaysLapsed,
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
        _settings.DryRun = model.DryRun;
        _settings.StoreName = model.StoreName ?? string.Empty;
        _settings.AzureOpenAIEndpoint = model.AzureOpenAIEndpoint?.Trim() ?? string.Empty;
        _settings.AzureOpenAIApiKey = model.AzureOpenAIApiKey?.Trim() ?? string.Empty;
        _settings.DeploymentName = model.DeploymentName?.Trim() ?? string.Empty;
        _settings.UseAzureKeyVault = model.UseAzureKeyVault;
        _settings.AzureKeyVaultUrl = model.AzureKeyVaultUrl?.Trim() ?? string.Empty;
        _settings.AzureKeyVaultSecretName = model.AzureKeyVaultSecretName?.Trim() ?? string.Empty;
        _settings.FromEmail = model.FromEmail?.Trim() ?? string.Empty;
        _settings.FromName = model.FromName?.Trim() ?? string.Empty;
        _settings.Email1DaysLapsed = model.Email1DaysLapsed;
        _settings.DaysBetweenEmail1And2 = model.DaysBetweenEmail1And2;
        _settings.DaysBetweenEmail2And3 = model.DaysBetweenEmail2And3;
        _settings.MaxDaysLapsed = model.MaxDaysLapsed;
        _settings.Email3DiscountCode = model.Email3DiscountCode?.Trim() ?? string.Empty;

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

    public async Task<IActionResult> Upcoming()
    {
        var upcomingEmails = await _winbackEmailService.GetUpcomingEmailsAsync();
        return View("~/Plugins/Marketing.WinbackEmail/Views/Upcoming.cshtml", upcomingEmails);
    }
}
