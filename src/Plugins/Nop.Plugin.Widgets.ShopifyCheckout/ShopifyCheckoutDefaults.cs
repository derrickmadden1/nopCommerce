namespace Nop.Plugin.Widgets.ShopifyCheckout;

/// <summary>
/// Represents plugin constants and defaults
/// </summary>
public static class ShopifyCheckoutDefaults
{
    /// <summary>
    /// System name for the plugin
    /// </summary>
    public static string SystemName => "Widgets.ShopifyCheckout";

    /// <summary>
    /// Name of the generic attribute used to store Shopify Variant ID / GID
    /// </summary>
    public static string ShopifyVariantIdAttribute => "ShopifyVariantId";

    /// <summary>
    /// Default Shopify Storefront API version
    /// </summary>
    public static string DefaultApiVersion => "2024-07";

    /// <summary>
    /// Configuration route name
    /// </summary>
    public static string ConfigurationRouteName => "Plugin.Widgets.ShopifyCheckout.Configure";

    /// <summary>
    /// Checkout initiation route name
    /// </summary>
    public static string CheckoutRouteName => "Plugin.Widgets.ShopifyCheckout.InitCheckout";
}
