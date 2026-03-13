using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Plugins;

namespace Nop.Plugin.Search.AzureAI;

public class AzureAISearchPlugin : BasePlugin, ISearchProvider
{
    private readonly AzureAISearchService _searchService;
    private readonly AzureAISearchSettings _settings;
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;

    public AzureAISearchPlugin(
        AzureAISearchService searchService,
        AzureAISearchSettings settings,
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _searchService = searchService;
        _settings = settings;
        _settingService = settingService;
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

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<AzureAISearchSettings>();
        await base.UninstallAsync();
    }
}