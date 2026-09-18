using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.AgentSearch
{
    /// <summary>
    /// Represents AzureSearch configuration section in App_Data/appsettings.json.
    /// Implements IConfig so nopCommerce AppSettings automatically binds it.
    /// </summary>
    public class AzureSearchConfig : IConfig
    {
        public string ServiceEndpoint { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}
