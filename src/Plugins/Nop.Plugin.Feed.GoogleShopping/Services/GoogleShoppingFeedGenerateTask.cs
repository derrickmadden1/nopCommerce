using System;
using System.Threading.Tasks;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Nop.Plugin.Feed.GoogleShopping.Services;

/// <summary>
/// Represents a schedule task to generate Google Shopping feeds
/// </summary>
public class GoogleShoppingFeedGenerateTask : IScheduleTask
{
    private readonly IPluginService _pluginService;
    private readonly IStoreService _storeService;

    public GoogleShoppingFeedGenerateTask(
        IPluginService pluginService,
        IStoreService storeService)
    {
        _pluginService = pluginService;
        _storeService = storeService;
    }

    /// <summary>
    /// Execute task
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task ExecuteAsync()
    {
        var pluginDescriptor = await _pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>("Feed.GoogleShopping");
        if (pluginDescriptor == null || !pluginDescriptor.Installed || pluginDescriptor.Instance<IPlugin>() is not GoogleShoppingService plugin)
        {
            return;
        }

        var stores = await _storeService.GetAllStoresAsync();
        foreach (var store in stores)
        {
            try
            {
                await plugin.GenerateStaticFileAsync(store);
            }
            catch (Exception)
            {
                // Ignore exceptions on a per-store basis to prevent blocking other stores
            }
        }
    }
}
