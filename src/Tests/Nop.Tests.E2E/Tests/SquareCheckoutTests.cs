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
    [Description("Runs end-to-end guest checkout flow using Square Sandbox Web Payments SDK and verifies order placement")]
    public async Task Square_Sandbox_Checkout_Places_Order_Successfully()
    {
        var flow = new SquareSandboxCheckoutFlow(Settings, CheckoutAuthMode.Guest);
        var result = await flow.RunAsync(Page);

        TestContext.WriteLine($"Square Guest Flow Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Square guest checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }

    [Test]
    [Description("Runs end-to-end checkout flow as a returning customer logging in during checkout with saved address")]
    public async Task Square_Sandbox_Checkout_As_Returning_Customer_Places_Order_Successfully()
    {
        var flow = new SquareSandboxCheckoutFlow(Settings, CheckoutAuthMode.ReturningCustomer);
        var result = await flow.RunAsync(Page);

        TestContext.WriteLine($"Square Returning Customer Flow Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Square returning customer checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }

    [Test]
    [Description("Runs end-to-end checkout flow registering as a new customer during checkout")]
    public async Task Square_Sandbox_Checkout_As_New_Registered_Customer_Places_Order_Successfully()
    {
        var flow = new SquareSandboxCheckoutFlow(Settings, CheckoutAuthMode.NewCustomerRegistration);
        var result = await flow.RunAsync(Page);

        TestContext.WriteLine($"Square New Registered Customer Flow Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Square new registered customer checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
        });
    }
}
