using Nop.Core;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.Marketing.WinbackEmail.Tasks;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using System.Collections.Generic;

namespace Nop.Plugin.Marketing.WinbackEmail;

public class WinbackEmailPlugin : BasePlugin
{
    private readonly ISettingService _settingService;
    private readonly IScheduleTaskService _scheduleTaskService;
    private readonly IWebHelper _webHelper;
    private readonly ILocalizationService _localizationService;

    public WinbackEmailPlugin(
        ISettingService settingService,
        IScheduleTaskService scheduleTaskService,
        IWebHelper webHelper,
        ILocalizationService localizationService)
    {
        _settingService = settingService;
        _scheduleTaskService = scheduleTaskService;
        _webHelper = webHelper;
        _localizationService = localizationService;
    }

    public override string GetConfigurationPageUrl()
        => $"{_webHelper.GetStoreLocation()}Admin/WinbackEmail/Configure";

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new WinbackEmailSettings
        {
            Enabled = false,
            DeploymentName = "gpt-4o-mini",
            Email1DaysLapsed = 60,
            Email2DaysLapsed = 67,
            Email3DaysLapsed = 74
        });

        // Register the nightly scheduled task
        var task = await _scheduleTaskService.GetTaskByTypeAsync(
            typeof(WinbackEmailTask).FullName!);

        if (task == null)
        {
            await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
            {
                Name = "AI Winback Email",
                Seconds = 86400, // 24 hours
                Type = typeof(WinbackEmailTask).FullName!,
                Enabled = false, // Enable manually once configured
                StopOnError = false
            });
        }

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Marketing.WinbackEmail.Enabled"] = "Enable winback emails",
            ["Plugins.Marketing.WinbackEmail.StoreName"] = "Store Name",
            ["Plugins.Marketing.WinbackEmail.AzureOpenAIEndpoint"] = "Azure OpenAI Endpoint",
            ["Plugins.Marketing.WinbackEmail.AzureOpenAIApiKey"] = "Azure OpenAI API Key",
            ["Plugins.Marketing.WinbackEmail.DeploymentName"] = "Azure OpenAI Deployment Name",
            ["Plugins.Marketing.WinbackEmail.FromEmail"] = "From Email Address",
            ["Plugins.Marketing.WinbackEmail.FromName"] = "From Name",
            ["Plugins.Marketing.WinbackEmail.Email1DaysLapsed"] = "Days lapsed for Email 1",
            ["Plugins.Marketing.WinbackEmail.Email2DaysLapsed"] = "Days lapsed for Email 2",
            ["Plugins.Marketing.WinbackEmail.Email3DaysLapsed"] = "Days lapsed for Email 3",
            ["Plugins.Marketing.WinbackEmail.Email3DiscountCode"] = "Discount Code for Email 3"
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<WinbackEmailSettings>();

        var task = await _scheduleTaskService.GetTaskByTypeAsync(
            typeof(WinbackEmailTask).FullName!);

        if (task != null)
            await _scheduleTaskService.DeleteTaskAsync(task);

        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Marketing.WinbackEmail");

        await base.UninstallAsync();
    }
}
