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
        if (string.IsNullOrWhiteSpace(Settings.PayPal.SandboxBuyerEmail) ||
            string.IsNullOrWhiteSpace(Settings.PayPal.SandboxBuyerPassword))
        {
            Assert.Ignore("Skipping PayPal sandbox test because SandboxBuyerEmail / SandboxBuyerPassword is not configured in testsettings.json or environment variables.");
        }

        var flow = new PayPalSandboxCheckoutFlow(Settings);
        var result = await flow.RunAsync(Page);

        TestContext.WriteLine($"PayPal Flow Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"PayPal checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }
}
