using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.ShopifyCheckout;

/// <summary>
/// Represents plugin settings
/// </summary>
public class ShopifyCheckoutSettings : ISettings
{
    /// <summary>
    /// Gets or sets the Shopify Store URL (e.g. "your-store.myshopify.com" or custom domain)
    /// </summary>
    public string StoreUrl { get; set; }

    /// <summary>
    /// Gets or sets the Shopify Storefront API Access Token (for cart creation)
    /// </summary>
    public string StorefrontAccessToken { get; set; }

    /// <summary>
    /// Gets or sets the Shopify Admin API Access Token (for product catalog sync)
    /// </summary>
    public string AdminApiAccessToken { get; set; }

    /// <summary>
    /// Gets or sets the Shopify GraphQL API version (e.g. "2024-07")
    /// </summary>
    public string ApiVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to display the button on the shopping cart page
    /// </summary>
    public bool DisplayButtonOnShoppingCart { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to display the button on the payment method selection page
    /// </summary>
    public bool DisplayButtonOnPaymentMethod { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to fallback to SKU as Variant ID if no explicit mapping exists
    /// </summary>
    public bool FallbackToSkuAsVariantId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to automatically sync products to Shopify on create/update/delete
    /// </summary>
    public bool EnableAutoCatalogSync { get; set; }

    /// <summary>
    /// Gets or sets custom text for the checkout button
    /// </summary>
    public string CustomButtonText { get; set; }
}
