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
    [Description("Runs end-to-end guest checkout flow using standard offline/check money order payment method")]
    public async Task Manual_Payment_Checkout_Places_Order_Successfully()
    {
        var flow = new ManualSandboxCheckoutFlow(Settings, CheckoutAuthMode.Guest);
        var result = await flow.RunAsync(Page);

        if (!result.Success && result.ErrorMessage != null && result.ErrorMessage.Contains("No offline payment method", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore(result.ErrorMessage);
        }

        TestContext.WriteLine($"Manual Guest Flow Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Manual checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }

    [Test]
    [Description("Runs end-to-end checkout flow using offline payment method as a returning customer")]
    public async Task Manual_Payment_Checkout_As_Returning_Customer_Places_Order_Successfully()
    {
        var flow = new ManualSandboxCheckoutFlow(Settings, CheckoutAuthMode.ReturningCustomer);
        var result = await flow.RunAsync(Page);

        if (!result.Success && result.ErrorMessage != null && result.ErrorMessage.Contains("No offline payment method", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore(result.ErrorMessage);
        }

        TestContext.WriteLine($"Manual Returning Customer Flow Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Manual returning customer checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }

    [Test]
    [Description("Runs end-to-end checkout flow using offline payment method registering as a new customer during checkout")]
    public async Task Manual_Payment_Checkout_As_New_Registered_Customer_Places_Order_Successfully()
    {
        var flow = new ManualSandboxCheckoutFlow(Settings, CheckoutAuthMode.NewCustomerRegistration);
        var result = await flow.RunAsync(Page);

        if (!result.Success && result.ErrorMessage != null && result.ErrorMessage.Contains("No offline payment method", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore(result.ErrorMessage);
        }

        TestContext.WriteLine($"Manual New Customer Flow Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Manual new registered customer checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }
}
