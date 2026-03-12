using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Search.AzureAI.Models;
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

    public AzureAISearchController(
        AzureAISearchSettings settings,
        AzureAISearchService searchService,
        ISettingService settingService,
        INotificationService notificationService)
    {
        _settings = settings;
        _searchService = searchService;
        _settingService = settingService;
        _notificationService = notificationService;
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
}
