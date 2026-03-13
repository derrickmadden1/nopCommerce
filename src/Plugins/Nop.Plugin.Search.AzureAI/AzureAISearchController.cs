using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Search.AzureAI.Infrastructure;
using Nop.Plugin.Search.AzureAI.Messages;
using Nop.Plugin.Search.AzureAI.Models;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Search.AzureAI.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class AzureAISearchController : BasePluginController
{
    private readonly AzureAISearchSettings _settings;
    private readonly AzureAISearchService _searchService;
    private readonly ISettingService _settingService;
    private readonly INotificationService _notificationService;
    private readonly IProductService _productService;
    private readonly ServiceBusPublisher _publisher;

    public AzureAISearchController(
        AzureAISearchSettings settings,
        AzureAISearchService searchService,
        ISettingService settingService,
        INotificationService notificationService,
        IProductService productService,
        ServiceBusPublisher publisher)
    {
        _settings = settings;
        _searchService = searchService;
        _settingService = settingService;
        _notificationService = notificationService;
        _productService = productService;
        _publisher = publisher;
    }

    public IActionResult Configure()
    {
        var model = new ConfigurationModel
        {
            Enabled = _settings.Enabled,
            Endpoint = _settings.Endpoint,
            QueryApiKey = _settings.QueryApiKey,
            IndexName = _settings.IndexName,
            ServiceBusConnectionString = _settings.ServiceBusConnectionString,
            ServiceBusQueueName = _settings.ServiceBusQueueName,
            PageSize = _settings.PageSize
        };

        return View("~/Plugins/Search.AzureAI/Views/Configure.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Plugins/Search.AzureAI/Views/Configure.cshtml", model);

        _settings.Enabled = model.Enabled;
        _settings.Endpoint = model.Endpoint.Trim();
        _settings.QueryApiKey = model.QueryApiKey.Trim();
        _settings.IndexName = model.IndexName.Trim();
        _settings.ServiceBusConnectionString = model.ServiceBusConnectionString.Trim();
        _settings.ServiceBusQueueName = model.ServiceBusQueueName.Trim();
        _settings.PageSize = model.PageSize;

        await _settingService.SaveSettingAsync(_settings);

        // Force search client to rebuild with new settings
        _searchService.ResetClient();

        _notificationService.SuccessNotification("Azure AI Search settings saved.");

        return RedirectToAction("Configure");
    }

    [HttpPost, ActionName("Configure")]
    [FormValueRequired("sync")]
    public async Task<IActionResult> Sync()
    {
        try
        {
            // 1. Get all IDs from the Azure AI Search Index
            var indexedIds = await _searchService.GetAllIndexedIdsAsync();

            // 2. Identify stale products (in index but no longer in nopCommerce DB)
            var dbProducts = await _productService.GetProductsByIdsAsync(indexedIds.ToArray());
            var dbIds = dbProducts.Select(p => p.Id).ToHashSet();

            var staleCount = 0;
            foreach (var id in indexedIds.Where(id => !dbIds.Contains(id)))
            {
                await _publisher.PublishAsync(new ProductIndexMessage 
                { 
                    ProductId = id, 
                    Action = ProductIndexAction.Delete,
                    OccurredAtUtc = DateTime.UtcNow
                });
                staleCount++;
            }

            // 3. Fully re-index all current published products to ensure they are sync'd
            var allProducts = await _productService.SearchProductsAsync(showHidden: false);
            var indexCount = 0;
            foreach (var product in allProducts)
            {
                var message = await _searchService.PrepareProductIndexMessageAsync(product, ProductIndexAction.Index);
                await _publisher.PublishAsync(message);
                indexCount++;
            }

            _notificationService.SuccessNotification($"Sync initiated. Removed {staleCount} stale products and queued {indexCount} products for re-indexing.");
        }
        catch (Exception ex)
        {
            _notificationService.ErrorNotification($"Sync failed: {ex.Message}");
        }

        return RedirectToAction("Configure");
    }
}
