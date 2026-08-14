using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Models;

/// <summary>
/// Represents plugin configuration model
/// </summary>
public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Widgets.ShopifyCheckout.Fields.StoreUrl")]
    public string StoreUrl { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.ShopifyCheckout.Fields.StorefrontAccessToken")]
    public string StorefrontAccessToken { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.ShopifyCheckout.Fields.ApiVersion")]
    public string ApiVersion { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.ShopifyCheckout.Fields.DisplayButtonOnShoppingCart")]
    public bool DisplayButtonOnShoppingCart { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.ShopifyCheckout.Fields.DisplayButtonOnPaymentMethod")]
    public bool DisplayButtonOnPaymentMethod { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.ShopifyCheckout.Fields.FallbackToSkuAsVariantId")]
    public bool FallbackToSkuAsVariantId { get; set; }

    [NopResourceDisplayName("Plugins.Widgets.ShopifyCheckout.Fields.CustomButtonText")]
    public string CustomButtonText { get; set; }
}
