using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Helpers;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker
{
    public class CheckoutAbandonmentTrackerPlugin : BasePlugin, IMiscPlugin
    {
        private readonly IMigrationManager _migrationManager;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly IWebHelper _webHelper;

        private const string AbandonedCheckoutTaskType =
            "Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services.AbandonedCheckoutTask, Nop.Plugin.Misc.CheckoutAbandonmentTracker";

        public CheckoutAbandonmentTrackerPlugin(
            IMigrationManager migrationManager,
            IScheduleTaskService scheduleTaskService,
            IWebHelper webHelper)
        {
            _migrationManager = migrationManager;
            _scheduleTaskService = scheduleTaskService;
            _webHelper = webHelper;
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/CheckoutAbandonmentTracker/List";
        }

        public override async Task InstallAsync()
        {
            // Creates the CheckoutAttempt table via CheckoutAttemptBuilder
            _migrationManager.ApplyUpMigrations(GetType().Assembly);

            // Register the scheduled task programmatically so it's active
            // immediately on install, not just when an admin manually adds it.
            if (await _scheduleTaskService.GetTaskByTypeAsync(AbandonedCheckoutTaskType) == null)
            {
                await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
                {
                    Name = "Flag abandoned checkouts",
                    Seconds = 1800, // 30 minutes
                    Type = AbandonedCheckoutTaskType,
                    Enabled = true,
                    StopOnError = false
                });
            }

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            var task = await _scheduleTaskService.GetTaskByTypeAsync(AbandonedCheckoutTaskType);
            if (task != null)
                await _scheduleTaskService.DeleteTaskAsync(task);

            // Deliberately NOT dropping the CheckoutAttempt table (see
            // SchemaMigration.Down and README) so re-enabling the plugin
            // later doesn't lose historical abandonment data.
            await base.UninstallAsync();
        }
    }
}



