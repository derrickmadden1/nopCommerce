using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.PayPalCommerce.Models.Public;

/// <summary>
/// Represents the Fastlane view model for the shopping cart page
/// </summary>
public record FastlaneCartModel : BaseNopModel
{
    public string ScriptUrl { get; set; }
    public string ClientToken { get; set; }
    public string CustomerEmail { get; set; }
    public bool CustomerHasEmail { get; set; }
    public bool IsGuest { get; set; }
    public bool FastlaneRequireEmailIntercept { get; set; }
    public string CaptureCustomerUrl { get; set; }
}
