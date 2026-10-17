using Microsoft.Extensions.Configuration;

namespace Nop.Tests.E2E.Configuration;

public class CheckoutTestSettings
{
    public string BaseUrl { get; set; } = "https://localhost:58552";
    public bool Headless { get; set; } = false;
    public int SlowMo { get; set; } = 300;
    public string TestProductSlug { get; set; } = "system-verification-item";
    public CustomerSettings Customer { get; set; } = new();
    public SquareSettings Square { get; set; } = new();
    public PayPalSettings PayPal { get; set; } = new();
}

public class CustomerSettings
{
    public string FirstName { get; set; } = "Test";
    public string LastName { get; set; } = "Buyer";
    public string Email { get; set; } = "e2e-test@rosecottagecroft.co.uk";
    public string Company { get; set; } = "Rose Cottage Test Ltd";
    public string Address1 { get; set; } = "123 Test Lane";
    public string City { get; set; } = "Testville";
    public string County { get; set; } = "Testshire";
    public string ZipPostalCode { get; set; } = "TE5 5ST";
    public string PhoneNumber { get; set; } = "07700900123";
    public string Country { get; set; } = "United Kingdom";
    public string ReturningCustomerEmail { get; set; } = "e2e-returning-customer@rosecottagecroft.co.uk";
    public string ReturningCustomerPassword { get; set; } = "Password123!";
}

public class SquareSettings
{
    public string TestCardNumber { get; set; } = "4111111111111111";
    public string Expiration { get; set; } = "12/28";
    public string Cvv { get; set; } = "123";
    public string PostalCode { get; set; } = "TE5 5ST";
    public bool Handle3ds { get; set; } = true;
    public string ThreeDsSmsCode { get; set; } = "1234";
}

public class PayPalSettings
{
    public string SandboxBuyerEmail { get; set; } = "";
    public string SandboxBuyerPassword { get; set; } = "";
}

public static class TestConfig
{
    private static CheckoutTestSettings? _settings;

    public static CheckoutTestSettings Current => _settings ??= Load();

    public static CheckoutTestSettings Load()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("testsettings.template.json", optional: true)
            .AddJsonFile("testsettings.json", optional: true)
            .AddEnvironmentVariables("CHECKOUT_");

        var config = builder.Build();
        var settings = new CheckoutTestSettings();
        config.GetSection("CheckoutTest").Bind(settings);

        // Fallback overrides from environment variables if present
        var envBaseUrl = Environment.GetEnvironmentVariable("CHECKOUT_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envBaseUrl))
            settings.BaseUrl = envBaseUrl.TrimEnd('/');

        var envHeadless = Environment.GetEnvironmentVariable("CHECKOUT_HEADLESS");
        if (bool.TryParse(envHeadless, out var headless))
            settings.Headless = headless;

        var envPayPalEmail = Environment.GetEnvironmentVariable("PAYPAL_SANDBOX_BUYER_EMAIL");
        if (!string.IsNullOrWhiteSpace(envPayPalEmail))
            settings.PayPal.SandboxBuyerEmail = envPayPalEmail;

        var envPayPalPassword = Environment.GetEnvironmentVariable("PAYPAL_SANDBOX_BUYER_PASSWORD");
        if (!string.IsNullOrWhiteSpace(envPayPalPassword))
            settings.PayPal.SandboxBuyerPassword = envPayPalPassword;

        return settings;
    }
}
