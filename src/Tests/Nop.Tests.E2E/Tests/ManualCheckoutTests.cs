using Nop.Tests.E2E.Fixtures;
using Nop.Tests.E2E.Flows;
using NUnit.Framework;

namespace Nop.Tests.E2E.Tests;

[TestFixture]
[Category("CheckoutE2E")]
[Category("Manual")]
public class ManualCheckoutTests : CheckoutTestBase
{
    [Test]
    [Description("Runs end-to-end checkout flow using standard offline/manual payment method to verify core checkout pipeline")]
    public async Task Manual_Payment_Checkout_Places_Order_Successfully()
    {
        var flow = new ManualSandboxCheckoutFlow(Settings);
        var result = await flow.RunAsync(Page);

        if (!result.Success && result.ErrorMessage != null && result.ErrorMessage.Contains("No offline payment method", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore(result.ErrorMessage);
        }

        TestContext.WriteLine($"Manual Flow Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Manual checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }
}
