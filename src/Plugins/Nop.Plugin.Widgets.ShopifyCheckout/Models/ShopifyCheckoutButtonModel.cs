using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Models;

/// <summary>
/// Represents public button view model
/// </summary>
public record ShopifyCheckoutButtonModel : BaseNopModel
{
    public string ButtonText { get; set; }
    public string CustomCssClass { get; set; }
    public string InitCheckoutUrl { get; set; }
}
