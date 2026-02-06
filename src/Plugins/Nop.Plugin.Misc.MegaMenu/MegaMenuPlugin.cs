using Nop.Services.Common;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.MegaMenu
{
    public class MegaMenuPlugin : BasePlugin, IMiscPlugin
    {
        public override string GetConfigurationPageUrl()
            => string.Empty; // no config page

        public override async Task InstallAsync()
        {
            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            await base.UninstallAsync();
        }
    }
}