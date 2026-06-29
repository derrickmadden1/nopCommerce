public class Ap2CheckoutRequest
{
    public string Sku { get; set; }
    public int Quantity { get; set; }
    public string Email { get; set; }
    public Ap2Address ShippingAddress { get; set; }
    public string PaymentToken { get; set; } // The cryptographic authorization token
}

public class Ap2Address
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Address1 { get; set; }
    public string City { get; set; }
    public string ZipPostalCode { get; set; }
    public string CountryTwoLetterIsoCode { get; set; }
}
