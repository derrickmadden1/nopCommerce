using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Data.Migrations;
using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.ReviewReward
{
    public class ReviewRewardPlugin : BasePlugin, IMiscPlugin
    {
        private readonly IMigrationManager _migrationManager;
        private readonly ILocalizationService _localizationService;

        public ReviewRewardPlugin(
            IMigrationManager migrationManager,
            ILocalizationService localizationService)
        {
            _migrationManager = migrationManager;
            _localizationService = localizationService;
        }

        public override async Task InstallAsync()
        {
            _migrationManager.ApplyUpMigrations(GetType().Assembly);

            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Misc.ReviewReward.MarketCode"] = "Market Purchase Code",
                ["Plugins.Misc.ReviewReward.MarketCode.Hint"] = "Enter the purchase code provided at the market stall to verify your review.",
                ["Plugins.Misc.ReviewReward.MarketCode.Invalid"] = "The market purchase code entered is invalid or has expired."
            });

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            _migrationManager.ApplyDownMigrations(GetType().Assembly);

            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.ReviewReward");

            await base.UninstallAsync();
        }
    }
}
