using Nop.Plugin.Marketing.WinbackEmail.Services;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Marketing.WinbackEmail.Tasks;

/// <summary>
/// Scheduled task — runs nightly and processes winback emails for all sequence steps.
/// Register via nopCommerce admin: Configuration → Schedule Tasks
/// </summary>
public class WinbackEmailTask : IScheduleTask
{
    private readonly WinbackEmailService _winbackEmailService;

    public WinbackEmailTask(WinbackEmailService winbackEmailService)
    {
        _winbackEmailService = winbackEmailService;
    }

    public async Task ExecuteAsync()
    {
        await _winbackEmailService.ProcessWinbacksAsync();
    }
}
