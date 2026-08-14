using System.Threading.Tasks;
using Nop.Plugin.Widgets.ShopifyCheckout.Models;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Services;

/// <summary>
/// Represents the Shopify order and inventory sync service interface
/// </summary>
public interface IShopifyOrderSyncService
{
    /// <summary>
    /// Syncs a Shopify order and decrements local nopCommerce product/combination inventory
    /// </summary>
    /// <param name="order">Shopify order payload</param>
    /// <returns>Result containing success flag and log message</returns>
    Task<(bool Success, string Message, int SyncedItemsCount)> ProcessShopifyOrderAsync(ShopifyWebhookOrderModel order);
}
