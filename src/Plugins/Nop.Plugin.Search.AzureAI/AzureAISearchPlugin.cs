using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace Nop.Plugin.Search.AzureAI;

public class AzureAISearchPlugin : BasePlugin, ISearchProvider
{
    private readonly AzureAISearchService _searchService;
    private readonly AzureAISearchSettings _settings;
    private readonly ISettingService _settingService;
    private readonly ILocalizationService _localizationService;
    private readonly IWebHelper _webHelper;

    public AzureAISearchPlugin(
        AzureAISearchService searchService,
        AzureAISearchSettings settings,
        ISettingService settingService,
        ILocalizationService localizationService,
        IWebHelper webHelper)
    {
        _searchService = searchService;
        _settings = settings;
        _settingService = settingService;
        _localizationService = localizationService;
        _webHelper = webHelper;
    }

    /// <summary>
    /// Called by nopCommerce when a customer performs a product search.
    /// Returns matching product IDs in relevance order.
    /// </summary>
    public async Task<List<int>> SearchProductsAsync(string keywords, bool isLocalized)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(keywords))
            return new List<int>();

        var (productIds, _) = await _searchService.SearchAsync(
            query: keywords,
            pageIndex: 0,
            pageSize: _settings.PageSize
        );

        return productIds;
    }

    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/AzureAISearch/Configure";
    }

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new AzureAISearchSettings
        {
            Enabled = false,
            IndexName = "products",
            PageSize = 20
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Search.AzureAI.Enabled"] = "Enabled",
            ["Plugins.Search.AzureAI.Endpoint"] = "Azure Search Endpoint",
            ["Plugins.Search.AzureAI.QueryApiKey"] = "Query API Key",
            ["Plugins.Search.AzureAI.IndexName"] = "Index Name",
            ["Plugins.Search.AzureAI.ServiceBusConnectionString"] = "Service Bus Connection String",
            ["Plugins.Search.AzureAI.ServiceBusQueueName"] = "Service Bus Queue Name",
            ["Plugins.Search.AzureAI.PageSize"] = "Page Size"
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<AzureAISearchSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Search.AzureAI");
        await base.UninstallAsync();
    }
}