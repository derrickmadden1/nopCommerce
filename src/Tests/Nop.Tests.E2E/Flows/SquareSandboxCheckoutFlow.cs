using System.Diagnostics;
using Microsoft.Playwright;
using Nop.Tests.E2E.Configuration;
using NUnit.Framework;

namespace Nop.Tests.E2E.Flows;

public class SquareSandboxCheckoutFlow : BaseCheckoutFlow
{
    public CheckoutAuthMode AuthMode { get; }

    public override string RouteName => $"square-sandbox-checkout-{AuthMode.ToString().ToLowerInvariant()}";

    public SquareSandboxCheckoutFlow(CheckoutTestSettings? settings = null, CheckoutAuthMode authMode = CheckoutAuthMode.Guest) 
        : base(settings)
    {
        AuthMode = authMode;
    }

    public override async Task<CheckoutResult> RunAsync(IPage page, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Add item to cart
            await AddTestProductToCartAsync(page);

            // 2. Go to cart and accept terms
            await ProceedToCartAndAcceptTermsAsync(page);

            // 3. Handle authentication step (Guest, Returning Customer, or New Registration)
            await HandleAuthenticationStepAsync(page, AuthMode);

            // 4. Fill Billing & Shipping addresses
            await FillBillingAddressIfRequiredAsync(page);
            await FillShippingAddressIfRequiredAsync(page);

            // 5. Select Shipping method
            await SelectShippingMethodAsync(page);

            // 6. Select Square Payment Method
            await SelectPaymentMethodAsync(page, "Payments.Square");

            // 7. Fill Square Payment Info (Web Payments SDK iframe)
            await FillSquarePaymentDetailsAsync(page);

            // 8. Confirm Order
            await ConfirmOrderAsync(page);

            // 9. Assert order completed
            var orderNumber = await WaitForOrderCompletionAndGetNumberAsync(page);
            stopwatch.Stop();

            return new CheckoutResult(
                RouteName,
                Success: true,
                OrderNumber: orderNumber,
                Duration: stopwatch.Elapsed,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CheckoutResult(
                RouteName,
                Success: false,
                OrderNumber: null,
                Duration: stopwatch.Elapsed,
                ErrorMessage: ex.Message);
        }
    }
}
