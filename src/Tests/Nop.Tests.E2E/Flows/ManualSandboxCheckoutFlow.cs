using System.Diagnostics;
using Microsoft.Playwright;
using Nop.Tests.E2E.Configuration;
using NUnit.Framework;

namespace Nop.Tests.E2E.Flows;

public class ManualSandboxCheckoutFlow : BaseCheckoutFlow
{
    public CheckoutAuthMode AuthMode { get; }

    public override string RouteName => $"manual-checkout-{AuthMode.ToString().ToLowerInvariant()}";

    public ManualSandboxCheckoutFlow(CheckoutTestSettings? settings = null, CheckoutAuthMode authMode = CheckoutAuthMode.Guest) 
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

            // 3. Handle auth step (Guest, Returning Customer, or New Registration)
            await HandleAuthenticationStepAsync(page, AuthMode);

            // 4. Fill Billing & Shipping addresses
            await FillBillingAddressIfRequiredAsync(page);
            await FillShippingAddressIfRequiredAsync(page);

            // 5. Select Shipping method
            await SelectShippingMethodAsync(page);

            // 6. Select Manual / Check payment method
            await SelectManualPaymentMethodAsync(page);

            // 7. Payment Info step
            await ConfirmPaymentInfoStepAsync(page);

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

    private async Task SelectManualPaymentMethodAsync(IPage page)
    {
        if (!page.Url.Contains("/checkout/paymentmethod", StringComparison.OrdinalIgnoreCase))
            return;

        await DismissCookieNotificationAsync(page);

        // Try selecting Payments.CheckMoneyOrder or Payments.Manual
        var manualRadio = page.Locator("input[value='Payments.CheckMoneyOrder'], input[value='Payments.Manual']").First;
        if (await manualRadio.CountAsync() > 0)
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Selecting offline manual payment method");
            await manualRadio.CheckAsync(new() { Force = true });
        }
        else
        {
            throw new InvalidOperationException("No offline payment method (Payments.CheckMoneyOrder or Payments.Manual) is enabled on this store. Enable Check / Money Order in Admin -> Configuration -> Payment Methods to test offline manual checkout.");
        }

        var nextBtn = page.Locator(".payment-method-page button[name='nextstep'], #payment-method-buttons-container .payment-method-next-step-button, button[onclick*='PaymentMethod.save']").First;
        await nextBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await nextBtn.ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/checkout/paymentmethod", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    private async Task ConfirmPaymentInfoStepAsync(IPage page)
    {
        if (!page.Url.Contains("/checkout/paymentinfo", StringComparison.OrdinalIgnoreCase))
            return;

        await DismissCookieNotificationAsync(page);

        var paymentInfoNext = page.Locator(".payment-info-page button[name='nextstep'], .payment-info-next-step-button, #payment-info-buttons-container .payment-info-next-step-button, button[onclick*='PaymentInfo.save']").First;
        try
        {
            await paymentInfoNext.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await paymentInfoNext.ClickAsync();
            await page.WaitForURLAsync(url => !url.Contains("/checkout/paymentinfo", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        }
        catch
        {
            // Payment info step might have been skipped automatically
        }
    }
}
