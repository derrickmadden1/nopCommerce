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

    [Test]
    [Description("Verifies that Guest checkout does not display the 'Save card' checkbox or stored cards dropdown, preventing guest card-on-file errors")]
    public async Task Square_Guest_Checkout_Does_Not_Display_Save_Card_Option()
    {
        var flow = new SquareSandboxCheckoutFlow(Settings, CheckoutAuthMode.Guest);
        await flow.NavigateToPaymentInfoAsync(Page, CheckoutAuthMode.Guest, "Payments.Square");

        // Verify we are on paymentinfo
        Assert.That(Page.Url, Does.Contain("/checkout/paymentinfo"));

        // Assert SaveCard checkbox is not present in DOM for guests
        var saveCardInput = Page.Locator("#SaveCard, input[name='SaveCard']");
        var saveCardCount = await saveCardInput.CountAsync();
        Assert.That(saveCardCount, Is.EqualTo(0), "Guest checkout must not display the SaveCard checkbox.");

        // Assert StoredCard dropdown is not present in DOM for guests
        var storedCardDropdown = Page.Locator("#StoredCardId, select[name='StoredCardId']");
        var storedCardCount = await storedCardDropdown.CountAsync();
        Assert.That(storedCardCount, Is.EqualTo(0), "Guest checkout must not display the StoredCard dropdown.");

        // Cleanup cart
        await flow.ClearCartAsync(Page);
    }

    [Test]
    [Order(1)]
    [Description("Verifies PR #269 fix: checking 'Save card' during checkout executes payment first and saves card on file without 'Card nonce already used' error")]
    public async Task Square_Sandbox_Checkout_With_Save_Card_Option_Places_Order_Without_Nonce_Error()
    {
        var flow = new SquareSandboxCheckoutFlow(Settings, CheckoutAuthMode.NewCustomerRegistration, saveCard: true);
        var result = await flow.RunAsync(Page);

        TestContext.WriteLine($"Square Save Card Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Square save-card checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
            Assert.That(result.ErrorMessage ?? string.Empty, Does.Not.Contain("Card nonce already used"), "Should not encounter card nonce reuse error from PR #269.");
        });
    }

    [Test]
    [Description("Verifies PR #269 stored card flow: customer selects previously saved card from #StoredCardId and places order without re-entering card details")]
    public async Task Square_Sandbox_Checkout_Using_Stored_Card_Places_Order_Successfully()
    {
        // 1. Seed customer with a saved card on file
        TestContext.WriteLine("Square Stored Card Test: Step 1 - Registering customer and saving card on file...");
        var seedFlow = new SquareSandboxCheckoutFlow(Settings, CheckoutAuthMode.NewCustomerRegistration, saveCard: true);
        var seedResult = await seedFlow.RunAsync(Page);
        Assert.That(seedResult.Success, Is.True, $"Step 1 failed: Seeding customer and saving card did not succeed: {seedResult.ErrorMessage}");
        TestContext.WriteLine($"Step 1 complete: Seed order placed (OrderNumber: {seedResult.OrderNumber}). Now placing second order using stored card...");

        // Ensure at least 65 seconds have elapsed since seed order was created to satisfy MinimumOrderPlacementInterval
        TestContext.WriteLine("Waiting 45s for MinimumOrderPlacementInterval cooldown before placing second order...");
        await Task.Delay(TimeSpan.FromSeconds(45));

        // 2. In the same authenticated session, place a second order selecting the stored card from #StoredCardId
        var storedCardFlow = new SquareSandboxCheckoutFlow(Settings, CheckoutAuthMode.ReturningCustomer, useStoredCard: true);
        var result = await storedCardFlow.RunAsync(Page);

        TestContext.WriteLine($"Square Stored Card Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Square stored card checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number from completed page.");
            Assert.That(result.ErrorMessage ?? string.Empty, Does.Not.Contain("Card nonce already used"), "Should not encounter card nonce reuse error.");
        });
    }

    [Test]
    [Description("Verifies PR #269 error handling: declined card displays user-friendly error message, session is cleared, and technical nonces/tokens are not leaked")]
    public async Task Square_Sandbox_Declined_Card_Shows_User_Friendly_Error_And_Does_Not_Leak_Nonce()
    {
        // Card number 4000 0000 0000 0002 triggers GENERIC_DECLINE in Square Sandbox
        var flow = new SquareSandboxCheckoutFlow(
            Settings, 
            CheckoutAuthMode.Guest, 
            customCardNumber: "4000000000000002", 
            expectPaymentFailure: true);

        var result = await flow.RunAsync(Page);

        TestContext.WriteLine($"Square Decline Test Result: Success={result.Success}, ErrorMessage='{result.ErrorMessage}', Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, "Expected flow to capture payment decline error on confirm step.");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty, "Expected user-facing error message.");
            
            // Check that the error message is user-friendly (mapped by FormatCustomerPaymentError)
            Assert.That(result.ErrorMessage, Does.Contain("declined").IgnoreCase.Or.Contain("issue processing your payment").IgnoreCase,
                $"Expected friendly decline error message, but received: '{result.ErrorMessage}'");

            // Verify technical tokens and nonces are NOT leaked
            Assert.That(result.ErrorMessage, Does.Not.Contain("cnon:"), "Raw card nonce should not be exposed to user.");
            Assert.That(result.ErrorMessage, Does.Not.Contain("source_id"), "Internal source_id should not be exposed to user.");
            Assert.That(result.ErrorMessage, Does.Not.Contain("idempotency"), "Internal idempotency key should not be exposed to user.");
        });

        // Cleanup
        await flow.ClearCartAsync(Page);
    }
}
