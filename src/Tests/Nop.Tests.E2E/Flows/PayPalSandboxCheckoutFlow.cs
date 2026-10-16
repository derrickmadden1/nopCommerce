using System.Diagnostics;
using Microsoft.Playwright;
using Nop.Tests.E2E.Configuration;
using NUnit.Framework;

namespace Nop.Tests.E2E.Flows;

public class PayPalSandboxCheckoutFlow : BaseCheckoutFlow
{
    public override string RouteName => "paypal-sandbox-checkout";

    public PayPalSandboxCheckoutFlow(CheckoutTestSettings? settings = null) : base(settings)
    {
    }

    public override async Task<CheckoutResult> RunAsync(IPage page, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var paypal = Settings.PayPal;
            if (string.IsNullOrWhiteSpace(paypal.SandboxBuyerEmail) || string.IsNullOrWhiteSpace(paypal.SandboxBuyerPassword))
            {
                throw new InvalidOperationException("PayPal sandbox credentials are not set in testsettings.json or PAYPAL_SANDBOX_BUYER_EMAIL/PAYPAL_SANDBOX_BUYER_PASSWORD environment variables.");
            }

            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Starting PayPal Sandbox Checkout Flow");

            // 1. Add item to cart
            await AddTestProductToCartAsync(page);

            // 2. Go to cart and accept terms
            var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigating to cart: {cartUrl}");
            await page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });
            await DismissCookieNotificationAsync(page);

            var termsCheckbox = page.Locator("#termsofservice");
            if (await termsCheckbox.IsVisibleAsync())
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Checking terms of service checkbox");
                await termsCheckbox.CheckAsync();
            }

            // 3. PayPal Smart button can appear on cart or OPC checkout step
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for PayPal Smart Button iframe");
            var paypalFrame = page.FrameLocator("iframe[name^='__zoid__paypal_buttons'], .paypal-button-container iframe.component-frame, iframe[title='PayPal']");
            var paypalButton = paypalFrame.Locator("[data-funding-source='paypal'], .paypal-button").First;

            await paypalButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 25000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] PayPal button visible, preparing popup listener and clicking button");

            var popupTask = page.WaitForPopupAsync(new() { Timeout = 15000 });
            await paypalButton.ClickAsync();

            IPage paypalPage;
            try
            {
                paypalPage = await popupTask;
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] PayPal popup opened, url={paypalPage.Url}");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Popup wait timed out or failed ({ex.Message}), falling back to main page");
                paypalPage = page;
            }

            // 4. Handle PayPal Login / Checkout
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for PayPal popup to navigate...");
            await paypalPage.WaitForURLAsync(url => !url.Equals("about:blank", StringComparison.OrdinalIgnoreCase), new() { Timeout = 30000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] PayPal popup navigated to: {paypalPage.Url}");

            await paypalPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await Task.Delay(2000); // Allow Hermes React to hydrate

            // If the popup loaded the Guest Checkout page (with 'Create Account' and a 'Log In' button),
            // click 'Log In' to navigate to the buyer member login screen.
            var guestLogInBtn = paypalPage.Locator("button:has-text('Log In')").First;
            var isGuestCheckout = (await paypalPage.Locator("button:has-text('Create Account')").CountAsync() > 0) ||
                                  paypalPage.Url.Contains("checkoutnow", StringComparison.OrdinalIgnoreCase);

            if (isGuestCheckout && await guestLogInBtn.IsVisibleAsync())
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Popup opened to guest checkout; clicking 'Log In' button to go to member login");
                await guestLogInBtn.ClickAsync();
                await paypalPage.WaitForURLAsync(url => url.Contains("prompt=login") || url.Contains("hermes") || url.Contains("signin"), new() { Timeout = 25000 });
                await paypalPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await Task.Delay(2000);
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigated to login screen: {paypalPage.Url}");
            }

            // Step A: Buyer email
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for buyer login email field");
            var emailInput = paypalPage.Locator("#email, input[name='login_email'], input[type='email']").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Entering PayPal sandbox buyer email: {paypal.SandboxBuyerEmail}");
            await emailInput.FillAsync(paypal.SandboxBuyerEmail);

            // If a 'Next' button is present (2-step login flow), click it
            var nextBtn = paypalPage.Locator("button:has-text('Next'), #btnNext, button#btnNext").First;
            if (await nextBtn.IsVisibleAsync())
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking Next button on login screen");
                await nextBtn.ClickAsync();
                await Task.Delay(1500);
            }

            // Step B: Buyer password
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for password field to be visible");
            var passwordInput = paypalPage.Locator("#password, input[name='login_password'], input[type='password']").First;
            await passwordInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 25000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Entering PayPal buyer password");
            await passwordInput.FillAsync(paypal.SandboxBuyerPassword);

            // Step C: Click Log In
            var loginBtn = paypalPage.Locator("button:has-text('Log In'), #btnLogin, button#btnLogin").First;
            await loginBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking Log In button");
            await loginBtn.ClickAsync();

            // 5. Complete / Confirm payment in PayPal
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for payment confirmation button in PayPal");
            var payButtonRegex = new System.Text.RegularExpressions.Regex(@"^(Pay|Pay Now|Complete Purchase|Agree and Continue|Agree & Continue|Save and Continue|Continue to Review Order)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var submitBtn = paypalPage.GetByRole(AriaRole.Button, new() { NameRegex = payButtonRegex }).Last;

            try
            {
                await submitBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
            }
            catch
            {
                // Fallback to CSS selectors if GetByRole doesn't locate it
                submitBtn = paypalPage.Locator("button:has-text('Complete Purchase'), button:has-text('Pay Now'), button:has-text('Agree and Continue'), button:has-text('Agree & Continue'), button:has-text('Save and Continue'), #payment-submit-btn, button[name='payment-submit-btn'], [data-testid='submit-button-initial']").Last;
                await submitBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
            }

            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking PayPal confirmation button");
            await submitBtn.ClickAsync();

            // If a secondary review/continue button appears, handle it
            try
            {
                var secondaryBtn = paypalPage.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex(@"^(Pay|Pay Now|Complete Purchase)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).Last;
                if (await secondaryBtn.CountAsync() > 0 && await secondaryBtn.IsVisibleAsync())
                {
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking secondary Pay button");
                    await secondaryBtn.ClickAsync();
                }
            }
            catch
            {
                // Ignored
            }

            // 6. Wait for store order-completed return on main page (or ConfirmOrder intermediate step)
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for main page to transition to order confirmation or completion");

            var redirectStart = DateTime.UtcNow;
            while (DateTime.UtcNow - redirectStart < TimeSpan.FromSeconds(45))
            {
                if (page.Url.Contains("ConfirmOrder", StringComparison.OrdinalIgnoreCase))
                {
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Store reached ConfirmOrder page, clicking confirm button");
                    var confirmBtn = page.Locator(".confirm-order-next-step-button, button[name='nextstep']").First;
                    await confirmBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
                    await confirmBtn.ClickAsync();
                    break;
                }

                if (page.Url.Contains("/checkout/completed", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                await Task.Delay(1000);
            }

            var orderNumber = await WaitForOrderCompletionAndGetNumberAsync(page, timeoutSeconds: 45);
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Order completed successfully! Order Number: {orderNumber}");
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
