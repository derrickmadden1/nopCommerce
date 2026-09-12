using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace Nop.Plugin.Widgets.AgentSearch
{
    /// <summary>
    /// Exposes the store's existing Azure AI Search index to external AI agents
    /// via a structured JSON API (POST /api/agent/search).
    /// </summary>
    public class AgentSearchPlugin : BasePlugin, IMiscPlugin
    {
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly IWebHelper _webHelper;

        public AgentSearchPlugin(
            ISettingService settingService,
            ILocalizationService localizationService,
            IWebHelper webHelper)
        {
            _settingService = settingService;
            _localizationService = localizationService;
            _webHelper = webHelper;
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/AgentSearch/Configure";
        }

        public override async Task InstallAsync()
        {
            var settings = new AgentSearchSettings
            {
                Enabled = true,
                MaxResultsDefault = 10,
                MaxResultsCap = 25,
                RequireApiKey = false, // flip on once you're ready to gate access
                ApiKey = string.Empty,
                UseQueryUnderstanding = false // toggles the LLM constraint-extraction pass
            };
            await _settingService.SaveSettingAsync(settings);

            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Widgets.AgentSearch.Enabled"] = "Enable agent search endpoint",
                ["Plugins.Widgets.AgentSearch.MaxResultsDefault"] = "Default max results",
                ["Plugins.Widgets.AgentSearch.MaxResultsCap"] = "Hard cap on max results",
                ["Plugins.Widgets.AgentSearch.RequireApiKey"] = "Require API key",
                ["Plugins.Widgets.AgentSearch.ApiKey"] = "API key",
                ["Plugins.Widgets.AgentSearch.UseQueryUnderstanding"] = "Use LLM query understanding"
            });

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            await _settingService.DeleteSettingAsync<AgentSearchSettings>();
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Widgets.AgentSearch");
            await base.UninstallAsync();
        }
    }
}
