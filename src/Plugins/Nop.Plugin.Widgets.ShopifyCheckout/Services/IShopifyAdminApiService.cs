using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Core.Domain.Catalog;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Services;

/// <summary>
/// Represents the Shopify Admin API catalog synchronization service interface
/// </summary>
public interface IShopifyAdminApiService
{
    /// <summary>
    /// Pushes a product to Shopify Admin API and saves the resulting Variant GID to GenericAttributes
    /// </summary>
    /// <param name="product">Product entity</param>
    /// <returns>Result containing success flag, mapped Variant GID, and message</returns>
    Task<(bool Success, string VariantGid, string Message)> CreateOrUpdateProductAsync(Product product);

    /// <summary>
    /// Pushes a product attribute combination to Shopify and maps its Variant GID
    /// </summary>
    /// <param name="product">Parent product entity</param>
    /// <param name="combination">Product attribute combination entity</param>
    /// <returns>Result containing success flag, mapped Variant GID, and message</returns>
    Task<(bool Success, string VariantGid, string Message)> CreateOrUpdateCombinationAsync(Product product, ProductAttributeCombination combination);

    /// <summary>
    /// Deletes a product mapping from Shopify Admin API
    /// </summary>
    /// <param name="product">Product entity</param>
    /// <returns>Result containing success flag and message</returns>
    Task<(bool Success, string Message)> DeleteProductAsync(Product product);

    /// <summary>
    /// Executes a full catalog sync pushing all active nopCommerce products & combinations to Shopify
    /// </summary>
    /// <returns>Sync summary results</returns>
    Task<(int TotalProcessed, int SyncedCount, int FailedCount, List<string> Logs)> FullCatalogSyncAsync();

    /// <summary>
    /// Auto-generates or retrieves a Storefront API Access Token using Admin API
    /// </summary>
    /// <returns>Result containing success flag, token string, and message</returns>
    Task<(bool Success, string Token, string Message)> GetOrCreateStorefrontAccessTokenAsync();

    /// <summary>
    /// Clears any in-memory cached access token so subsequent API requests fetch a fresh token
    /// </summary>
    void ClearTokenCache();

    /// <summary>
    /// Creates a Shopify Draft Order with custom item prices and returns the invoice checkout URL
    /// </summary>
    /// <param name="items">List of items containing Variant GID, quantity, and custom unit price</param>
    /// <param name="customerEmail">Customer email address</param>
    /// <returns>Result containing success flag, invoice URL, and error message</returns>
    Task<(bool Success, string InvoiceUrl, string Message)> CreateDraftOrderAsync(
        IEnumerable<(string VariantGid, int Quantity, decimal UnitPrice, decimal OriginalListPrice)> items,
        string customerEmail = null,
        decimal orderDiscountAmount = 0,
        int? nopCustomerId = null);

    /// <summary>
    /// Creates a fulfillment on Shopify with tracking information
    /// </summary>
    Task<(bool Success, string Message)> CreateFulfillmentAsync(
        long shopifyOrderId,
        string trackingNumber,
        string trackingCompany = null,
        string trackingUrl = null,
        bool notifyCustomer = true);
}
