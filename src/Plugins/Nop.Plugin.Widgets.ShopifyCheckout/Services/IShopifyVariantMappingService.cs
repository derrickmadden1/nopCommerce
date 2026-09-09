using System.Threading.Tasks;
using Nop.Core.Domain.Orders;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Services;

/// <summary>
/// Represents the Shopify variant mapping service interface
/// </summary>
public interface IShopifyVariantMappingService
{
    /// <summary>
    /// Gets the Shopify Variant GID for a given shopping cart item
    /// </summary>
    /// <param name="item">Shopping cart item</param>
    /// <returns>Shopify Variant GID (e.g. "gid://shopify/ProductVariant/123456789") or null if unmapped</returns>
    Task<string> GetShopifyVariantGidAsync(ShoppingCartItem item);
}
