using Nop.Core;
using Nop.Services.Plugins;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using System.Collections.Generic;

namespace Nop.Plugin.Misc.UniversalCommerce;

public class UniversalCommercePlugin : BasePlugin
{
    private readonly IWebHelper _webHelper;
    private readonly ILocalizationService _localizationService;

    public UniversalCommercePlugin(
        IWebHelper webHelper,
        ILocalizationService localizationService)
    {
        _webHelper = webHelper;
        _localizationService = localizationService;
    }

    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/UcpAdmin/Configure";
    }

    public override async Task InstallAsync()
    {
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Misc.UniversalCommerce.Fields.Enabled"] = "Enable Google Agentic Commerce",
            ["Plugins.Misc.UniversalCommerce.Fields.ProtocolVersion"] = "Protocol Version (e.g. ap2)",
            ["Plugins.Misc.UniversalCommerce.Fields.AllowAutonomousCheckout"] = "Allow Autonomous Checkout",
            ["Plugins.Misc.UniversalCommerce.Fields.PermitLimit"] = "Rate Limit (Permits per Window)",
            ["Plugins.Misc.UniversalCommerce.Fields.WindowInSeconds"] = "Rate Limit Window (Seconds)",
            ["Plugins.Misc.UniversalCommerce.Fields.QueueLimit"] = "Rate Limit Queue Size"
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.UniversalCommerce");

        await base.UninstallAsync();
    }
}
