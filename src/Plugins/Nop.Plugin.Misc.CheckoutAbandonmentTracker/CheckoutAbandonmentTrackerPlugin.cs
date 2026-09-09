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
using Nop.Services.Security;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker
{
    public class CheckoutAbandonmentTrackerPlugin : BasePlugin, IMiscPlugin, IAdminMenuPlugin
    {
        private readonly IMigrationManager _migrationManager;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly IPermissionService _permissionService;
        private readonly IWebHelper _webHelper;

        private const string AbandonedCheckoutTaskType =
            "Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services.AbandonedCheckoutTask, Nop.Plugin.Misc.CheckoutAbandonmentTracker";

        public CheckoutAbandonmentTrackerPlugin(
            IMigrationManager migrationManager,
            IScheduleTaskService scheduleTaskService,
            IPermissionService permissionService,
            IWebHelper webHelper)
        {
            _migrationManager = migrationManager;
            _scheduleTaskService = scheduleTaskService;
            _permissionService = permissionService;
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

        public async Task ManageSiteMapAsync(AdminMenuItem rootNode)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
                return;

            var pluginNode = new AdminMenuItem
            {
                SystemName = "Misc.CheckoutAbandonmentTracker",
                Title = "Abandoned Checkouts",
                Url = "/Admin/CheckoutAbandonmentTracker/List",
                IconClass = "far fa-dot-circle",
                Visible = true
            };
            rootNode.ChildNodes.Add(pluginNode);
        }
    }
}



