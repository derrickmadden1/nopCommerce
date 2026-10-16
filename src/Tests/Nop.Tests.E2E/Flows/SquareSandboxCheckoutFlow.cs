using System.Diagnostics;
using Microsoft.Playwright;
using Nop.Tests.E2E.Configuration;
using NUnit.Framework;

namespace Nop.Tests.E2E.Flows;

public class SquareSandboxCheckoutFlow : BaseCheckoutFlow
{
    public override string RouteName => "square-sandbox-checkout";

    public SquareSandboxCheckoutFlow(CheckoutTestSettings? settings = null) : base(settings)
    {
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

            // 3. Handle guest checkout if prompted
            await HandleGuestCheckoutIfPromptedAsync(page);

            // 4. Fill Billing & Shipping addresses
            await FillBillingAddressIfRequiredAsync(page);
            await FillShippingAddressIfRequiredAsync(page);

            // 5. Select Shipping method
            await SelectShippingMethodAsync(page);

            // 6. Select Square Payment Method
            await SelectSquarePaymentMethodAsync(page);

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

    private async Task SelectSquarePaymentMethodAsync(IPage page)
    {
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] SelectSquarePaymentMethodAsync entering, url={page.Url}");
        if (!page.Url.Contains("/checkout/paymentmethod", StringComparison.OrdinalIgnoreCase))
            return;

        await DismissCookieNotificationAsync(page);

        // Wait for payment method step container
        var squareRadio = page.Locator("input[value='Payments.Square']").First;
        await squareRadio.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Selecting Payments.Square payment method");
        if (!await squareRadio.IsCheckedAsync())
        {
            await squareRadio.CheckAsync(new() { Force = true });
        }

        var nextBtn = page.Locator(".payment-method-page button[name='nextstep'], #payment-method-buttons-container .payment-method-next-step-button").Locator(":visible").First;
        await nextBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking payment method Next button");
        await nextBtn.ClickAsync(new() { Force = true });

        await page.WaitForURLAsync(url => !url.Contains("/checkout/paymentmethod", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Left paymentmethod page, new url={page.Url}");
    }

    private async Task FillSquarePaymentDetailsAsync(IPage page)
    {
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] FillSquarePaymentDetailsAsync entering, url={page.Url}");
        if (!page.Url.Contains("/checkout/paymentinfo", StringComparison.OrdinalIgnoreCase))
            return;

        await DismissCookieNotificationAsync(page);

        // Wait for Square card container iframe
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for Square #card-container iframe");
        var cardIframe = page.FrameLocator("#card-container iframe");

        var cardNumberInput = cardIframe.Locator("input[name='cardNumber'], input[autocomplete='cc-number'], input[placeholder*='Card number']").First;
        await cardNumberInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 25000 });

        var square = Settings.Square;
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Entering Square test card: {square.TestCardNumber[..4]}****");

        // Fill Card Number
        await cardNumberInput.ClickAsync();
        await cardNumberInput.FillAsync(square.TestCardNumber);

        // Fill Expiration
        var expInput = cardIframe.Locator("input[name='expirationDate'], input[autocomplete='cc-exp'], input[placeholder*='MM/YY']").First;
        if (await expInput.IsVisibleAsync())
        {
            await expInput.ClickAsync();
            await expInput.FillAsync(square.Expiration);
        }

        // Fill CVV
        var cvvInput = cardIframe.Locator("input[name='cvv'], input[autocomplete='cc-csc'], input[placeholder*='CVV']").First;
        if (await cvvInput.IsVisibleAsync())
        {
            await cvvInput.ClickAsync();
            await cvvInput.FillAsync(square.Cvv);
        }

        // Fill Postal Code if visible inside the card element
        var postalInput = cardIframe.Locator("input[name='postalCode'], input[autocomplete='postal-code'], input[placeholder*='Postal']").First;
        if (await postalInput.IsVisibleAsync())
        {
            await postalInput.ClickAsync();
            await postalInput.FillAsync(square.PostalCode);
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Postal code field filled with {square.PostalCode}");
        }

        // Click Custom #pay-button (which calls Square tokenize() / verifyBuyer() and clicks payment-info-next-step-button)
        var payButton = page.Locator("#pay-button").First;
        await payButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking #pay-button for Square tokenization");
        await payButton.ClickAsync();

        // Check if 3DS CardinalCommerce challenge appears
        if (square.Handle3ds)
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Checking for 3DS CardinalCommerce modal");
            await HandleCardinal3dsModalIfPresentAsync(page, square.ThreeDsSmsCode);
        }

        // Wait for step transition to confirm order
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for transition away from paymentinfo");
        await page.WaitForURLAsync(url => !url.Contains("/checkout/paymentinfo", StringComparison.OrdinalIgnoreCase), new() { Timeout = 25000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Transitioned to confirm, new url={page.Url}");
    }

    private async Task HandleCardinal3dsModalIfPresentAsync(IPage page, string smsCode)
    {
        try
        {
            // Cardinal frame may take a couple of seconds to mount
            var cardinalFrame = page.FrameLocator("#Cardinal-CCA-IFrame");

            // Wait briefly to see if 3DS modal challenge input appears
            var challengeInput = cardinalFrame.Locator("input[name='challengeDataEntry'], input[type='password'], input[name='txtOtpCode'], input[placeholder*='code']").First;
            await challengeInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 6000 });

            if (await challengeInput.IsVisibleAsync())
            {
                await challengeInput.FillAsync(smsCode);

                var submitBtn = cardinalFrame.Locator("input[type='submit'], button:has-text('Submit'), button:has-text('SUBMIT'), #btnSubmit").First;
                if (await submitBtn.IsVisibleAsync())
                {
                    await submitBtn.ClickAsync();
                    await Task.Delay(2000);
                }
            }
        }
        catch
        {
            // 3DS was either not challenged, completed automatically, or had a direct continue button
            try
            {
                var cardinalFrame = page.FrameLocator("#Cardinal-CCA-IFrame");
                var directSubmit = cardinalFrame.Locator("input[value='SUBMIT'], button:has-text('SUBMIT'), button:has-text('Submit')").First;
                await directSubmit.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 2000 });
                await directSubmit.ClickAsync();
                await Task.Delay(2000);
            }
            catch
            {
                // No challenge modal
            }
        }
    }

    private async Task ConfirmOrderAsync(IPage page)
    {
        if (!page.Url.Contains("/checkout/confirm", StringComparison.OrdinalIgnoreCase))
            return;

        await DismissCookieNotificationAsync(page);

        // On Confirm step, check #termsofservice if present
        var termsCheckbox = page.Locator("#termsofservice");
        if (await termsCheckbox.CountAsync() > 0)
        {
            try
            {
                await termsCheckbox.CheckAsync(new() { Force = true });
            }
            catch
            {
                await page.EvaluateAsync("const cb = document.getElementById('termsofservice'); if (cb) cb.checked = true;");
            }
        }

        // Wait for confirm order button
        var confirmButton = page.Locator(".confirm-order-btn-modern, .order-confirm-page button[name='nextstep'], #confirm-order-buttons-container .confirm-order-next-step-button, button:has-text('Confirm')").Locator(":visible").First;
        await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking confirm order button");
        await confirmButton.ClickAsync(new() { Force = true });
    }
}
