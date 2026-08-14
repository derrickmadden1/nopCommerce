using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Common;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Services;

/// <summary>
/// Represents the Shopify variant mapping service
/// </summary>
public class ShopifyVariantMappingService : IShopifyVariantMappingService
{
    #region Fields

    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IProductAttributeParser _productAttributeParser;
    private readonly IProductService _productService;
    private readonly ShopifyCheckoutSettings _settings;

    #endregion

    #region Ctor

    public ShopifyVariantMappingService(
        IGenericAttributeService genericAttributeService,
        IProductAttributeParser productAttributeParser,
        IProductService productService,
        ShopifyCheckoutSettings settings)
    {
        _genericAttributeService = genericAttributeService;
        _productAttributeParser = productAttributeParser;
        _productService = productService;
        _settings = settings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the Shopify Variant GID for a given shopping cart item
    /// </summary>
    /// <param name="item">Shopping cart item</param>
    /// <returns>Shopify Variant GID (e.g. "gid://shopify/ProductVariant/123456789") or null if unmapped</returns>
    public async Task<string> GetShopifyVariantGidAsync(ShoppingCartItem item)
    {
        if (item == null)
            return null;

        var product = await _productService.GetProductByIdAsync(item.ProductId);
        if (product == null)
            return null;

        string variantId = null;

        // 1. Check if there are product attribute combinations
        if (!string.IsNullOrEmpty(item.AttributesXml))
        {
            var combination = await _productAttributeParser.FindProductAttributeCombinationAsync(product, item.AttributesXml);
            if (combination != null)
            {
                // Check GenericAttribute on ProductAttributeCombination
                variantId = await _genericAttributeService.GetAttributeAsync<ProductAttributeCombination, string>(
                    combination.Id, ShopifyCheckoutDefaults.ShopifyVariantIdAttribute);

                // Fallback to SKU on combination if enabled
                if (string.IsNullOrWhiteSpace(variantId) && _settings.FallbackToSkuAsVariantId)
                {
                    variantId = combination.Sku;
                }
            }
        }

        // 2. If no combination variant ID found, check GenericAttribute on Product
        if (string.IsNullOrWhiteSpace(variantId))
        {
            variantId = await _genericAttributeService.GetAttributeAsync<Product, string>(
                product.Id, ShopifyCheckoutDefaults.ShopifyVariantIdAttribute);
        }

        // 3. Fallback to Product.Sku if enabled
        if (string.IsNullOrWhiteSpace(variantId) && _settings.FallbackToSkuAsVariantId)
        {
            variantId = product.Sku;
        }

        if (string.IsNullOrWhiteSpace(variantId))
            return null;

        return FormatShopifyVariantGid(variantId.Trim());
    }

    /// <summary>
    /// Formats a raw variant ID or SKU into a Shopify Variant GID URI format
    /// </summary>
    /// <param name="rawVariantId">Raw variant string</param>
    /// <returns>Shopify Variant GID</returns>
    private static string FormatShopifyVariantGid(string rawVariantId)
    {
        if (string.IsNullOrWhiteSpace(rawVariantId))
            return null;

        // If it's already a full GID (e.g. gid://shopify/ProductVariant/123456)
        if (rawVariantId.StartsWith("gid://shopify/ProductVariant/", System.StringComparison.OrdinalIgnoreCase))
            return rawVariantId;

        // If it's pure digits, prepend the GID prefix
        if (Regex.IsMatch(rawVariantId, @"^\d+$"))
            return $"gid://shopify/ProductVariant/{rawVariantId}";

        // If it starts with gid://, trust it
        if (rawVariantId.StartsWith("gid://", System.StringComparison.OrdinalIgnoreCase))
            return rawVariantId;

        return null;
    }

    #endregion
}
