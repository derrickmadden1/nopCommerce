using Nop.Core;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.Marketing.WinbackEmail.Tasks;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Marketing.WinbackEmail;

public class WinbackEmailPlugin : BasePlugin
{
    private readonly ISettingService _settingService;
    private readonly IScheduleTaskService _scheduleTaskService;
    private readonly IWebHelper _webHelper;

    public WinbackEmailPlugin(
        ISettingService settingService,
        IScheduleTaskService scheduleTaskService,
        IWebHelper webHelper)
    {
        _settingService = settingService;
        _scheduleTaskService = scheduleTaskService;
        _webHelper = webHelper;
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

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<WinbackEmailSettings>();

        var task = await _scheduleTaskService.GetTaskByTypeAsync(
            typeof(WinbackEmailTask).FullName!);

        if (task != null)
            await _scheduleTaskService.DeleteTaskAsync(task);

        await base.UninstallAsync();
    }
}
