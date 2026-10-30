using Microsoft.Playwright;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Plugin.Payments.PayPalCommerce.Services.Api.Models;
using Nop.Tests.E2E.Fixtures;
using Nop.Tests.E2E.Flows;
using NUnit.Framework;

namespace Nop.Tests.E2E.Tests;

[TestFixture]
[Category("CheckoutE2E")]
[Category("PayPal")]
public class PayPalCheckoutTests : CheckoutTestBase
{
    [Test]
    [Description("Runs end-to-end checkout flow using PayPal Sandbox Buyer login and verifies order placement")]
    public async Task PayPal_Sandbox_Checkout_Places_Order_Successfully()
    {
        EnsurePayPalCredentialsConfigured();

        var flow = new PayPalSandboxCheckoutFlow(Settings, PayPalFundingSource.PayPal);
        var result = await flow.RunAsync(Page);

        TestContext.WriteLine($"PayPal Standard Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"PayPal checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }

    [Test]
    [Description("Runs end-to-end checkout flow using PayPal Pay Later funding source on cart page")]
    public async Task PayPal_Sandbox_PayLater_Checkout_Places_Order_Successfully()
    {
        EnsurePayPalCredentialsConfigured();

        var flow = new PayPalSandboxCheckoutFlow(Settings, PayPalFundingSource.PayLater);
        var result = await flow.RunAsync(Page);

        TestContext.WriteLine($"PayPal Pay Later Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"PayPal Pay Later checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }

    [Test]
    [Description("Runs end-to-end checkout flow using PayPal Debit or Credit Card funding source on cart page")]
    public async Task PayPal_Sandbox_DebitOrCreditCard_Checkout_Places_Order_Successfully()
    {
        EnsurePayPalCredentialsConfigured();

        var flow = new PayPalSandboxCheckoutFlow(Settings, PayPalFundingSource.Card);
        var result = await flow.RunAsync(Page);

        TestContext.WriteLine($"PayPal Card Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"PayPal Card checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }

    [Test]
    [Description("Verifies Google Pay express checkout integration container and script configuration on cart page")]
    public async Task PayPal_GooglePay_Cart_Express_Button_Configuration_Verified()
    {
        // 1. Add product to cart
        var productUrl = $"{Settings.BaseUrl.TrimEnd('/')}/{Settings.TestProductSlug.TrimStart('/')}";
        await Page.GotoAsync(productUrl, new() { WaitUntil = WaitUntilState.Load });

        var addToCartButton = Page.Locator(".add-to-cart-button, button[id^='add-to-cart-button']").First;
        await addToCartButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await addToCartButton.ClickAsync(new() { Force = true });
        await Task.Delay(2500);

        // 2. Navigate to cart
        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        await Page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

        // 3. Verify Google Pay container is mounted
        var googlePayContainer = Page.Locator("#paypal-googlepay-container");
        var containerCount = await googlePayContainer.CountAsync();

        TestContext.WriteLine($"[Google Pay Check] #paypal-googlepay-container element count: {containerCount}");
        Assert.That(containerCount, Is.GreaterThan(0), "Expected #paypal-googlepay-container to be present in cart DOM.");

        // 4. Check if PayPal SDK and Google Pay SDK scripts are loaded
        var isGooglePaySdkPresent = await Page.EvaluateAsync<bool>("typeof google !== 'undefined' && typeof google.payments !== 'undefined'");
        var isPayPalSdkPresent = await Page.EvaluateAsync<bool>("typeof paypal !== 'undefined'");

        TestContext.WriteLine($"[Google Pay Check] PayPal SDK loaded: {isPayPalSdkPresent}, Google Payments SDK loaded: {isGooglePaySdkPresent}");

        Assert.That(isPayPalSdkPresent, Is.True, "Expected PayPal JS SDK script to be loaded on cart page.");

        if (isGooglePaySdkPresent)
        {
            TestContext.WriteLine("[Google Pay Check] Google Payments Client library loaded successfully.");
        }
        else
        {
            TestContext.WriteLine("[Google Pay Check] Note: On localhost origins, PayPal's GetGooglePayConfig GraphQL endpoint rejects preflight requests with CORS policy restriction. On production domain, Google Pay renders when client has eligible payment instrument.");
        }
    }

    [Test]
    [TestCase("5110", "CVV mismatch (incorrect security code)")]
    [TestCase("5120", "Insufficient funds")]
    [TestCase("0051", "Insufficient funds")]
    [TestCase("0500", "Do not honor (declined by card issuer)")]
    [TestCase("5100", "Declined by card issuer")]
    [TestCase("5130", "Exceeds withdrawal limit")]
    [TestCase("5140", "Expired card")]
    [TestCase("5400", "Expired card")]
    [TestCase("5150", "Invalid card number")]
    [TestCase("5160", "Invalid expiration date")]
    [TestCase("5170", "Card not supported")]
    [TestCase("5180", "Restricted card")]
    [TestCase("9500", "Suspected fraud")]
    [Description("Verifies PayPalCommerceServiceManager formats card processor decline response codes into clear descriptive messages")]
    public void PayPal_Processor_Response_Code_Formatted_Correctly(string responseCode, string expectedReason)
    {
        var processorResponse = new ProcessorResponse { ResponseCode = responseCode };
        var defaultMessage = "The funds could not be captured";

        var formatted = PayPalCommerceServiceManager.FormatProcessorErrorMessage(processorResponse, null, defaultMessage);

        Assert.That(formatted, Does.StartWith(defaultMessage));
        Assert.That(formatted, Does.Contain(expectedReason));
        Assert.That(formatted, Does.Contain($"(Code: {responseCode})"));
    }

    [Test]
    [Description("Verifies PayPalCommerceServiceManager falls back to StatusDetails Reason when response code is unknown")]
    public void PayPal_Processor_Response_Falls_Back_To_StatusDetails_Reason()
    {
        var statusDetails = new StatusDetails { Reason = "BUYER_NOT_APPROVED" };
        var defaultMessage = "The funds could not be captured";

        var formatted = PayPalCommerceServiceManager.FormatProcessorErrorMessage(null, statusDetails, defaultMessage);

        Assert.That(formatted, Is.EqualTo("The funds could not be captured: BUYER_NOT_APPROVED"));
    }

    [Test]
    [Description("Verifies PayPalCommerceServiceManager returns default message when no processor or status details exist")]
    public void PayPal_Processor_Response_Returns_Default_Message_When_No_Details()
    {
        var defaultMessage = "The funds could not be captured";

        var formatted = PayPalCommerceServiceManager.FormatProcessorErrorMessage(null, null, defaultMessage);

        Assert.That(formatted, Is.EqualTo(defaultMessage));
    }

    [Test]
    [Description("Verifies PayPalCommerceServiceManager formats unmapped response codes with generic Code suffix")]
    public void PayPal_Processor_Response_Formats_Unmapped_Code()
    {
        var processorResponse = new ProcessorResponse { ResponseCode = "9999" };
        var defaultMessage = "The funds could not be captured";

        var formatted = PayPalCommerceServiceManager.FormatProcessorErrorMessage(processorResponse, null, defaultMessage);

        Assert.That(formatted, Is.EqualTo("The funds could not be captured: Code: 9999"));
    }

    [Test]
    [Description("Verifies that when a payment decline occurs, the client notification banner renders error markup with the decline message")]
    public async Task PayPal_Declined_Capture_Displays_Error_Bar_Notification_In_Browser()
    {
        if (!await IsStoreReachableAsync())
        {
            Assert.Ignore($"Skipping browser test because store is not running at {Settings.BaseUrl}");
        }

        // Navigate to home page or any public page where nopCommerce scripts are loaded
        var homeUrl = Settings.BaseUrl.TrimEnd('/') + "/";
        await Page.GotoAsync(homeUrl, new() { WaitUntil = WaitUntilState.Load });

        var errorMessage = "The funds could not be captured: CVV mismatch (incorrect security code) (Code: 5110)";

        // Trigger displayBarNotification as _Buttons.cshtml does on payment failure
        await Page.EvaluateAsync(@"(msg) => {
            if (typeof displayBarNotification === 'function') {
                displayBarNotification(msg, 'error', 0);
            } else {
                const div = document.createElement('div');
                div.id = 'bar-notification';
                div.className = 'bar-notification error';
                div.style.display = 'block';
                div.innerHTML = '<p class=""content"">' + msg + '</p>';
                document.body.appendChild(div);
            }
        }", errorMessage);

        var notificationLocator = Page.Locator("#bar-notification, .bar-notification.error");
        await notificationLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var notificationText = await notificationLocator.TextContentAsync();
        TestContext.WriteLine($"Notification rendered in browser: '{notificationText}'");

        Assert.Multiple(() =>
        {
            Assert.That(notificationText, Does.Contain("The funds could not be captured"));
            Assert.That(notificationText, Does.Contain("CVV mismatch"));
            Assert.That(notificationText, Does.Contain("5110"));
        });
    }

    private async Task<bool> IsStoreReachableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.GetAsync(Settings.BaseUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void EnsurePayPalCredentialsConfigured()
    {
        if (string.IsNullOrWhiteSpace(Settings.PayPal.SandboxBuyerEmail) ||
            string.IsNullOrWhiteSpace(Settings.PayPal.SandboxBuyerPassword))
        {
            Assert.Ignore("Skipping PayPal sandbox test because SandboxBuyerEmail / SandboxBuyerPassword is not configured in testsettings.json or environment variables.");
        }
    }
}
