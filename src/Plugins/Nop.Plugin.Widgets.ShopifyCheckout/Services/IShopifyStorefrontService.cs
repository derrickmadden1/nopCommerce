using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Services;

/// <summary>
/// Represents the Shopify Storefront API service interface
/// </summary>
public interface IShopifyStorefrontService
{
    /// <summary>
    /// Creates a Shopify cart via Storefront GraphQL API and returns the checkout URL
    /// </summary>
    /// <param name="lineItems">List of line items containing merchandiseId (variant GID) and quantity</param>
    /// <returns>Checkout URL if successful, error messages otherwise</returns>
    Task<(string CheckoutUrl, List<string> Errors)> CreateCartAsync(IEnumerable<(string MerchandiseId, int Quantity)> lineItems);
}
