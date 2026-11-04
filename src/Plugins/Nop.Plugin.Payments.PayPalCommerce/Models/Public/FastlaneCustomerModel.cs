namespace Nop.Plugin.Payments.PayPalCommerce.Models.Public;

/// <summary>
/// Represents customer and profile data captured through PayPal Fastlane
/// </summary>
public record FastlaneCustomerModel
{
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string PhoneNumber { get; set; }
    public string Address1 { get; set; }
    public string Address2 { get; set; }
    public string City { get; set; }
    public string StateProvinceCodeOrName { get; set; }
    public string ZipPostalCode { get; set; }
    public string CountryCode { get; set; }
    public bool Authenticated { get; set; }
    public string FastlaneCardId { get; set; }
    public string FastlaneCardBrand { get; set; }
    public string FastlaneCardLastDigits { get; set; }
}
