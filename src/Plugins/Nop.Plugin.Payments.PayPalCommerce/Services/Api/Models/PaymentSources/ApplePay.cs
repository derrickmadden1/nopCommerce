using Newtonsoft.Json;

namespace Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models.PaymentSources;

/// <summary>
/// Represents the Apple Pay payment source
/// </summary>
public class ApplePay
{
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }

    [JsonProperty(PropertyName = "email_address")]
    public string EmailAddress { get; set; }
}