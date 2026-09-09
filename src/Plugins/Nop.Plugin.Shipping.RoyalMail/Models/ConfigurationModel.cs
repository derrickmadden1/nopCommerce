using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Shipping.RoyalMail.Models;

/// <summary>
/// Represents Royal Mail plugin configuration model
/// </summary>
public record ConfigurationModel : BaseNopModel
{
    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Shipping.RoyalMail.Fields.UseSandbox")]
    public bool UseSandbox { get; set; }
    public bool UseSandbox_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Shipping.RoyalMail.Fields.ClientId")]
    public string ClientId { get; set; }
    public bool ClientId_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Shipping.RoyalMail.Fields.ClientSecret")]
    public string ClientSecret { get; set; }
    public bool ClientSecret_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Shipping.RoyalMail.Fields.UseWebTrackingUrlFallback")]
    public bool UseWebTrackingUrlFallback { get; set; }
    public bool UseWebTrackingUrlFallback_OverrideForStore { get; set; }
}
