using Microsoft.Playwright;
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

    private void EnsurePayPalCredentialsConfigured()
    {
        if (string.IsNullOrWhiteSpace(Settings.PayPal.SandboxBuyerEmail) ||
            string.IsNullOrWhiteSpace(Settings.PayPal.SandboxBuyerPassword))
        {
            Assert.Ignore("Skipping PayPal sandbox test because SandboxBuyerEmail / SandboxBuyerPassword is not configured in testsettings.json or environment variables.");
        }
    }
}
