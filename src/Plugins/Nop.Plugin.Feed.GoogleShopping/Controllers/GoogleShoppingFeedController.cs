using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using Nop.Core;
using Nop.Core.Domain.Stores;
using Nop.Core.Infrastructure;
using Nop.Plugin.Feed.GoogleShopping.Services;
using Nop.Services.Configuration;
using Nop.Services.Plugins;
using Nop.Services.Stores;

namespace Nop.Plugin.Feed.GoogleShopping.Controllers;

public class GoogleShoppingFeedController : Controller
{
    private readonly ISettingService _settingService;
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;
    private readonly IPluginService _pluginService;
    private readonly INopFileProvider _nopFileProvider;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public GoogleShoppingFeedController(
        ISettingService settingService,
        IStoreContext storeContext,
        IStoreService storeService,
        IPluginService pluginService,
        INopFileProvider nopFileProvider,
        IWebHostEnvironment webHostEnvironment)
    {
        _settingService = settingService;
        _storeContext = storeContext;
        _storeService = storeService;
        _pluginService = pluginService;
        _nopFileProvider = nopFileProvider;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet("googleshopping/feed")]
    public async Task<IActionResult> GetFeed(int storeId = 0)
    {
        // Resolve store
        Store store = null;
        if (storeId > 0)
        {
            store = await _storeService.GetStoreByIdAsync(storeId);
        }
        if (store == null)
        {
            store = await _storeContext.GetCurrentStoreAsync();
        }
        if (store == null)
        {
            return NotFound("Store not found.");
        }

        // Load settings for the store
        var googleShoppingSettings = await _settingService.LoadSettingAsync<GoogleShoppingSettings>(store.Id);

        if (googleShoppingSettings.UseAzureBlobStorage)
        {
            if (string.IsNullOrEmpty(googleShoppingSettings.AzureBlobConnectionString) ||
                string.IsNullOrEmpty(googleShoppingSettings.AzureBlobContainerName))
            {
                return BadRequest("Azure Blob Storage is not fully configured.");
            }

            var fileName = $"{store.Id}-{googleShoppingSettings.StaticFileName}";
            var blobServiceClient = new BlobServiceClient(googleShoppingSettings.AzureBlobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(googleShoppingSettings.AzureBlobContainerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            return Redirect(blobClient.Uri.ToString());
        }
        else
        {
            // Local file serving fallback
            var localFilePath = _nopFileProvider.Combine(_webHostEnvironment.WebRootPath, "files", "exportimport", store.Id + "-" + googleShoppingSettings.StaticFileName);
            if (!_nopFileProvider.FileExists(localFilePath))
            {
                return NotFound("Feed file not found. Please generate the feed in the plugin administration first.");
            }

            var fileBytes = await _nopFileProvider.ReadAllBytesAsync(localFilePath);
            return File(fileBytes, "application/xml", store.Id + "-" + googleShoppingSettings.StaticFileName);
        }
    }
}
