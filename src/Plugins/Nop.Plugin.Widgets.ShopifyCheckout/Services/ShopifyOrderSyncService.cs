using System;
using System.Threading.Tasks;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Logging;
using Nop.Plugin.Widgets.ShopifyCheckout.Models;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Logging;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Services;

/// <summary>
/// Represents the Shopify order and inventory sync service
/// </summary>
public class ShopifyOrderSyncService : IShopifyOrderSyncService
{
    #region Fields

    private readonly IProductService _productService;
    private readonly IProductAttributeService _productAttributeService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ILogger _logger;
    private readonly ShopifyCheckoutSettings _settings;

    #endregion

    #region Ctor

    public ShopifyOrderSyncService(
        IProductService productService,
        IProductAttributeService productAttributeService,
        IGenericAttributeService genericAttributeService,
        ILogger logger,
        ShopifyCheckoutSettings settings)
    {
        _productService = productService;
        _productAttributeService = productAttributeService;
        _genericAttributeService = genericAttributeService;
        _logger = logger;
        _settings = settings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Syncs a Shopify order and decrements local nopCommerce product/combination inventory
    /// </summary>
    /// <param name="order">Shopify order payload</param>
    /// <returns>Result containing success flag and log message</returns>
    public async Task<(bool Success, string Message, int SyncedItemsCount)> ProcessShopifyOrderAsync(ShopifyWebhookOrderModel order)
    {
        if (order == null || order.LineItems == null)
            return (false, "Order payload is empty.", 0);

        int syncedCount = 0;

        foreach (var item in order.LineItems)
        {
            if (item.Quantity <= 0)
                continue;

            string variantGid = item.VariantId.HasValue ? $"gid://shopify/ProductVariant/{item.VariantId.Value}" : null;
            bool matched = false;

            // 1. Check ProductAttributeCombination by SKU
            if (!string.IsNullOrWhiteSpace(item.Sku))
            {
                var combination = await _productAttributeService.GetProductAttributeCombinationBySkuAsync(item.Sku.Trim());
                if (combination != null)
                {
                    combination.StockQuantity -= item.Quantity;
                    await _productAttributeService.UpdateProductAttributeCombinationAsync(combination);
                    syncedCount++;
                    matched = true;
                    await _logger.InformationAsync($"Shopify Order #{order.Name}: Decremented combination SKU '{combination.Sku}' stock by {item.Quantity}. New stock: {combination.StockQuantity}");
                }
            }

            // 2. Check Product by SKU if combination not found
            if (!matched && !string.IsNullOrWhiteSpace(item.Sku))
            {
                var product = await _productService.GetProductBySkuAsync(item.Sku.Trim());
                if (product != null)
                {
                    await _productService.AdjustInventoryAsync(product, -item.Quantity, message: $"Shopify Order #{order.Name}");
                    syncedCount++;
                    matched = true;
                    await _logger.InformationAsync($"Shopify Order #{order.Name}: Decremented product '{product.Name}' (SKU: '{product.Sku}') stock by {item.Quantity}. New stock: {product.StockQuantity}");
                }
            }

            if (!matched)
            {
                await _logger.WarningAsync($"Shopify Order #{order.Name}: Could not match item '{item.Title}' (SKU: '{item.Sku}', Variant ID: '{item.VariantId}') to nopCommerce catalog.");
            }
        }

        var message = $"Successfully processed Shopify Order #{order.Name}. Inventory updated for {syncedCount} of {order.LineItems.Count} line items.";
        await _logger.InformationAsync(message);

        return (true, message, syncedCount);
    }

    #endregion
}
