using Nop.Tests.E2E.Fixtures;
using Nop.Tests.E2E.Flows;
using NUnit.Framework;

namespace Nop.Tests.E2E.Tests;

[TestFixture]
[Category("CheckoutE2E")]
[Category("Square")]
public class SquareCheckoutTests : CheckoutTestBase
{
    [Test]
    [Description("Runs end-to-end checkout flow using Square Sandbox Web Payments SDK and verifies order placement")]
    public async Task Square_Sandbox_Checkout_Places_Order_Successfully()
    {
        var flow = new SquareSandboxCheckoutFlow(Settings);
        var result = await flow.RunAsync(Page);

        TestContext.WriteLine($"Square Flow Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Square checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }
}
