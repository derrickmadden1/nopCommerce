using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Shipping.RoyalMail.Models;

/// <summary>
/// Represents Royal Mail plugin configuration model
/// </summary>
public record ConfigurationModel : BaseNopModel
{
    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Shipping.RoyalMail.Fields.WebTrackingUrlFormat")]
    public string WebTrackingUrlFormat { get; set; }
    public bool WebTrackingUrlFormat_OverrideForStore { get; set; }
}
