using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Nop.Tests.E2E.Configuration;
using NUnit.Framework;

namespace Nop.Tests.E2E.Flows;

public class PayPalSandboxCheckoutFlow : BaseCheckoutFlow
{
    public PayPalFundingSource FundingSource { get; }

    public override string RouteName => $"paypal-sandbox-checkout-{FundingSource.ToString().ToLowerInvariant()}";

    public PayPalSandboxCheckoutFlow(CheckoutTestSettings? settings = null, PayPalFundingSource fundingSource = PayPalFundingSource.PayPal) 
        : base(settings)
    {
        FundingSource = fundingSource;
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

            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Starting PayPal Sandbox Checkout Flow ({FundingSource})");

            // 1. Add item to cart
            if (FundingSource == PayPalFundingSource.PayLater)
            {
                await EnsurePayLaterProductInCartAsync(page);
            }
            else
            {
                await AddTestProductToCartAsync(page);
            }

            // Ensure buyer-country=GB is passed to PayPal JS SDK in sandbox for UK Pay Later eligibility
            await page.RouteAsync("**/sdk/js*", async route =>
            {
                var url = route.Request.Url;
                if (!url.Contains("buyer-country="))
                {
                    var separator = url.Contains('?') ? "&" : "?";
                    var modifiedUrl = $"{url}{separator}buyer-country=GB";
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayPal SDK Route] Appending buyer-country=GB for sandbox funding eligibility");
                    await route.ContinueAsync(new() { Url = modifiedUrl });
                }
                else
                {
                    await route.ContinueAsync();
                }
            });

            // 2. Go to cart and accept terms
            var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigating to cart: {cartUrl}");
            await page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });
            await DismissCookieNotificationAsync(page);

            if (FundingSource == PayPalFundingSource.PayLater)
            {
                var cartRows = page.Locator("table.cart tbody tr");
                if (await cartRows.CountAsync() == 0)
                {
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Cart has 0 items on arrival, retrying product addition...");
                    await EnsurePayLaterProductInCartAsync(page);
                    await page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });
                    await DismissCookieNotificationAsync(page);
                }

                // If subtotal is £15.00, bump quantity with .quantity.up to ensure >= £30.00
                var subtotalElem = page.Locator(".order-subtotal .value-summary").First;
                if (await subtotalElem.CountAsync() > 0)
                {
                    var subtotalText = await subtotalElem.InnerTextAsync();
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Current cart subtotal: '{subtotalText}'");
                    if (subtotalText.Contains("15.00"))
                    {
                        var qtyUpBtn = page.Locator("table.cart .quantity.up, table.cart [id^='quantity-up-']").First;
                        if (await qtyUpBtn.CountAsync() > 0 && await qtyUpBtn.IsVisibleAsync())
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Clicking quantity-up to meet £30.00 threshold");
                            await qtyUpBtn.ClickAsync();
                            await Task.Delay(2500);
                        }
                    }
                }
            }

            var termsCheckbox = page.Locator("#termsofservice");
            if (await termsCheckbox.CountAsync() > 0)
            {
                if (!await termsCheckbox.IsCheckedAsync())
                {
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Checking terms of service checkbox");
                    await termsCheckbox.CheckAsync();
                    await page.EvaluateAsync("const cb = document.getElementById('termsofservice'); if (cb && !cb.checked) { cb.checked = true; cb.dispatchEvent(new Event('change', { bubbles: true })); }");
                }
            }

            // 3. Locate PayPal Smart Button based on funding source
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for PayPal Smart Button iframe");
            var paypalFrame = page.FrameLocator("iframe.component-frame:not(.invisible), iframe[name^='__zoid__paypal_buttons']:not([name*='prerender'])");

            ILocator paypalButton = FundingSource switch
            {
                PayPalFundingSource.PayLater => paypalFrame.Locator("[data-funding-source='paylater']").First,
                PayPalFundingSource.Card => paypalFrame.Locator("[data-funding-source='card']").First,
                _ => paypalFrame.Locator("[data-funding-source='paypal'], .paypal-button").First
            };

            await paypalButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 25000 });
            await Task.Delay(1500);

            var buttonBox = await paypalButton.BoundingBoxAsync();
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] PayPal {FundingSource} button visible and hydrated, bounding box: X={buttonBox?.X}, Y={buttonBox?.Y}, W={buttonBox?.Width}, H={buttonBox?.Height}");

            var buttonFrame = page.Frames.FirstOrDefault(f => f.Url.Contains("smart/buttons") || f.Url.Contains("paypal.com"));

            var popupTask = page.WaitForPopupAsync(new() { Timeout = 30000 });
            var contextPageTask = page.Context.WaitForPageAsync(new() { Timeout = 30000 });

            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Attempting click on PayPal button inside buttonFrame");
            if (buttonFrame != null)
            {
                var fundingAttr = FundingSource switch
                {
                    PayPalFundingSource.PayLater => "paylater",
                    PayPalFundingSource.Card => "card",
                    _ => "paypal"
                };
                
                // Try clicking the button locator in buttonFrame
                var btn = buttonFrame.Locator($"[data-funding-source='{fundingAttr}']");
                await btn.ClickAsync();

                // If popup didn't immediately appear, also trigger click via evaluate
                var earlyWait = await Task.WhenAny(popupTask, contextPageTask, Task.Delay(2000));
                if (earlyWait != popupTask && earlyWait != contextPageTask)
                {
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Triggering synthetic click via buttonFrame.EvaluateAsync");
                    await buttonFrame.EvaluateAsync($"document.querySelector(\"[data-funding-source='{fundingAttr}']\").click()");
                }
            }
            else
            {
                await paypalButton.ClickAsync();
            }

            IPage paypalPage;
            try
            {
                var winner = await Task.WhenAny(popupTask, contextPageTask);
                paypalPage = await winner;
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] PayPal popup opened, url={paypalPage.Url}");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Popup wait timed out or failed ({ex.Message}), checking context pages...");
                if (page.Context.Pages.Count > 1)
                {
                    paypalPage = page.Context.Pages[^1];
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Found open popup page in Context.Pages: {paypalPage.Url}");
                }
                else
                {
                    paypalPage = page;
                }
            }

            if (paypalPage.Url.EndsWith("/cart", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"PayPal popup window failed to open after clicking {FundingSource} button (stayed on {paypalPage.Url}).");
            }

            // 4. Handle PayPal Login / Checkout
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for PayPal popup to navigate...");
            await paypalPage.WaitForURLAsync(url => !url.Equals("about:blank", StringComparison.OrdinalIgnoreCase), new() { Timeout = 30000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] PayPal popup navigated to: {paypalPage.Url}");

            await paypalPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await Task.Delay(2000); // Allow Hermes React to hydrate

            // If the popup loaded the Guest Checkout page (with 'Create Account' and a 'Log In' button or link),
            // click 'Log In' to navigate to the buyer member login screen.
            var guestLogInBtn = paypalPage.Locator("a:has-text('Log In'), button:has-text('Log In'), [role='link']:has-text('Log In')").First;
            var isGuestCheckout = (await paypalPage.Locator("button:has-text('Create Account')").CountAsync() > 0) ||
                                  paypalPage.Url.Contains("checkoutnow", StringComparison.OrdinalIgnoreCase);

            if (isGuestCheckout && await guestLogInBtn.IsVisibleAsync())
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Popup opened to guest checkout; clicking 'Log In' button to go to member login");
                await guestLogInBtn.ClickAsync();
                try
                {
                    await paypalPage.WaitForURLAsync(url => url.Contains("prompt=login") || url.Contains("hermes") || url.Contains("signin"), new() { Timeout = 15000 });
                }
                catch { }
                await Task.Delay(2000);
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigated to login screen: {paypalPage.Url}");
            }

            // Step A: Buyer email (if visible)
            var emailInput = paypalPage.Locator("#email, input[name='login_email'], input[type='email']").First;
            try
            {
                await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
            }
            catch { }

            if (await emailInput.IsVisibleAsync())
            {
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
            }

            // Step B: Buyer password (if visible)
            var passwordInput = paypalPage.Locator("#password, input[name='login_password'], input[type='password']").First;
            if (await passwordInput.IsVisibleAsync())
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Entering PayPal buyer password");
                await passwordInput.FillAsync(paypal.SandboxBuyerPassword);

                // Step C: Click Log In
                var loginBtn = paypalPage.Locator("button:has-text('Log In'), #btnLogin, button#btnLogin").First;
                await loginBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking Log In button");
                await loginBtn.ClickAsync();
            }

            // 5. Complete / Confirm payment in PayPal
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for payment confirmation button in PayPal");
            var payButtonRegex = new Regex(@"^(Pay|Pay Now|Complete Purchase|Agree and Continue|Agree & Continue|Save and Continue|Continue to Review Order|Continue)$", RegexOptions.IgnoreCase);
            var submitBtn = paypalPage.GetByRole(AriaRole.Button, new() { NameRegex = payButtonRegex }).Last;

            try
            {
                await submitBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
            }
            catch
            {
                submitBtn = paypalPage.Locator("button:has-text('Complete Purchase'), button:has-text('Pay Now'), button:has-text('Agree and Continue'), button:has-text('Agree & Continue'), button:has-text('Save and Continue'), button:has-text('Continue'):not([name='continueshopping']):not(.continue-shopping-button):not(#btn-fastlane-skip):not(.fastlane-modal-skip), #payment-submit-btn, button[name='payment-submit-btn'], [data-testid='submit-button-initial']").Last;
                await submitBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
            }

            // Step D: Now that the review page is mounted with submitBtn visible, check if an option needs selection:
            try
            {
                var radioInfo = await paypalPage.EvaluateAsync<string>(@"() => {
                    const ariaRadios = Array.from(document.querySelectorAll('[role=""radio""]')).map(r => ({
                        text: (r.innerText || '').substring(0, 50),
                        ariaLabel: r.getAttribute('aria-label'),
                        checked: r.getAttribute('aria-checked'),
                        disabled: r.getAttribute('aria-disabled')
                    }));
                    const nativeRadios = Array.from(document.querySelectorAll('input[type=""radio""]')).map(r => ({
                        id: r.id,
                        value: r.value,
                        checked: r.checked,
                        disabled: r.disabled
                    }));
                    return JSON.stringify({ ariaRadios, nativeRadios });
                }");
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [RADIO DIAGNOSTICS] {radioInfo}");

            if (FundingSource == PayPalFundingSource.PayLater)
            {
                // Click the label or input for the active credit offer ("Pay Later" / "Pay in 30 Days")
                var creditOfferRadio = paypalPage.Locator("input[id^='credit-offer']:not([disabled])").First;
                if (await creditOfferRadio.CountAsync() > 0)
                {
                    var radioId = await creditOfferRadio.GetAttributeAsync("id") ?? "";
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Found Pay Later radio: #{radioId}");

                    var label = paypalPage.Locator($"label[for='{radioId}']");
                    if (await label.CountAsync() > 0 && await label.IsVisibleAsync())
                    {
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking label[for='{radioId}']");
                        await label.ClickAsync(new() { Force = true });
                    }
                    else
                    {
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking input#{radioId} directly");
                        await creditOfferRadio.ClickAsync(new() { Force = true });
                    }

                    // Also dispatch synthetic change/click via evaluate
                    await paypalPage.EvaluateAsync(@"(id) => {
                        const r = document.getElementById(id);
                        if (r) {
                            r.checked = true;
                            r.click();
                            const l = document.querySelector(`label[for=""${id}""]`);
                            if (l) l.click();
                            r.dispatchEvent(new Event('change', { bubbles: true }));
                        }
                    }", radioId);

                    await Task.Delay(2000);
                }
            }
            else if (FundingSource == PayPalFundingSource.Card)
            {
                // Select credit/debit card radio
                var cardRadio = paypalPage.Locator("input[id^='CC-']:not([disabled]), input[id*='card' i]:not([disabled])").First;
                if (await cardRadio.CountAsync() > 0)
                {
                    var cardRadioId = await cardRadio.GetAttributeAsync("id") ?? "";
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Selecting Card radio: #{cardRadioId}");
                    var label = paypalPage.Locator($"label[for='{cardRadioId}']");
                    if (await label.CountAsync() > 0 && await label.IsVisibleAsync())
                    {
                        await label.ClickAsync(new() { Force = true });
                    }
                    else
                    {
                        await cardRadio.ClickAsync(new() { Force = true });
                    }
                    await Task.Delay(1500);
                }
            }
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Plan selection note: {ex.Message}");
            }

            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking PayPal confirmation button");
            try
            {
                await submitBtn.ClickAsync(new() { Force = true });
            }
            catch (Exception exClick)
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Initial submit click note: {exClick.Message}, re-locating confirmation button");
                var freshSubmitBtn = paypalPage.Locator("button:has-text('Complete Purchase'), button:has-text('Pay Now'), button:has-text('Agree and Continue'), button:has-text('Agree & Continue'), button:has-text('Save and Continue'), button:has-text('Continue'), [role='button']:has-text('Continue'), #payment-submit-btn, button[name='payment-submit-btn']").Last;
                if (await freshSubmitBtn.CountAsync() > 0 && await freshSubmitBtn.IsVisibleAsync())
                {
                    await freshSubmitBtn.ClickAsync(new() { Force = true });
                }
            }

            if (FundingSource != PayPalFundingSource.PayLater)
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Payment submitted for {FundingSource}, waiting for PayPal popup to close...");
                var waitStart = DateTime.UtcNow;
                while (!paypalPage.IsClosed && DateTime.UtcNow - waitStart < TimeSpan.FromSeconds(30))
                {
                    if (page.Url.Contains("/checkout/completed") || page.Url.Contains("ConfirmOrder"))
                        break;
                    await Task.Delay(1000);
                }

                if (paypalPage.IsClosed)
                {
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] PayPal popup closed successfully!");
                }
                else
                {
                    // Check if an additional final submit button appeared
                    var extraSubmit = paypalPage.Locator("button:has-text('Complete Purchase'), button:has-text('Pay Now'), button:has-text('Pay')").Last;
                    if (await extraSubmit.CountAsync() > 0 && await extraSubmit.IsVisibleAsync())
                    {
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking extra final submit button in PayPal");
                        await extraSubmit.ClickAsync(new() { Force = true });
                    }
                }
            }
            else
            {
                // Handle subsequent multi-step screens for Pay Later (Screen 2: Loan overview, Screen 3: Details & Agreement)
                var lastActionTime = DateTime.UtcNow;
                var maxApprovalDuration = DateTime.UtcNow;
                while (!paypalPage.IsClosed && DateTime.UtcNow - lastActionTime < TimeSpan.FromSeconds(60) && DateTime.UtcNow - maxApprovalDuration < TimeSpan.FromMinutes(6))
                {
                    try
                    {
                        if (page.Url.Contains("/checkout/completed") || page.Url.Contains("ConfirmOrder"))
                            break;

                        // Locate active frame containing Pay Later content (Hermes may render modals in a child iframe, so check child frames before MainFrame)
                        IFrame targetFrame = paypalPage.MainFrame;
                        foreach (var frame in paypalPage.Frames.Reverse())
                        {
                            try
                            {
                                var fText = await frame.EvaluateAsync<string>("() => (document.body ? document.body.innerText : '') || ''");
                                if (fText.Contains("Confirm your details") || fText.Contains("Agree and Continue") || fText.Contains("What's your title") || 
                                    fText.Contains("Loan of") || fText.Contains("Billing address and phone number") || fText.Contains("Enter a valid phone number") ||
                                    fText.Contains("Link a card") || fText.Contains("Choose a way to pay"))
                                {
                                    targetFrame = frame;
                                    break;
                                }
                            }
                            catch { }
                        }

                        var screenText = await targetFrame.EvaluateAsync<string>("() => (document.body ? document.body.innerText : '') || ''");
                        var snippet = screenText.Length > 70 ? screenText[..70].Replace('\r', ' ').Replace('\n', ' ') : screenText.Replace('\r', ' ').Replace('\n', ' ');
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PAYPAL FRAME] url='{targetFrame.Url}', text='{snippet}'");

                    // Case 1: Terms / Details screen (Screen 3)
                    if (screenText.Contains("Confirm your details") || screenText.Contains("Agree and Continue") || screenText.Contains("What's your title") || screenText.Contains("What’s your title"))
                    {
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] On Pay Later 'Confirm your details' screen");

                        // 0. If an intro bottom sheet is showing (with 'Next' or 'close sheet')
                        foreach (var f in paypalPage.Frames)
                        {
                            try
                            {
                                var introNext = f.Locator("button:has-text('Next'), [role='button']:has-text('Next')").First;
                                if (await introNext.CountAsync() > 0 && await introNext.IsVisibleAsync())
                                {
                                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking intro sheet 'Next' button in frame {f.Url}");
                                    await introNext.ClickAsync(new() { Force = true });
                                    await Task.Delay(1500);
                                    break;
                                }
                            }
                            catch { }
                        }

                        // A. Fill Title dropdown across all frames
                        foreach (var f in paypalPage.Frames)
                        {
                            try
                            {
                                // 1. Check if native select exists
                                var selCount = await f.Locator("select").CountAsync();
                                if (selCount > 0)
                                {
                                    await f.EvaluateAsync(@"() => {
                                        const sel = document.querySelector('select');
                                        if (sel && sel.options.length > 1) {
                                            for (let i = 0; i < sel.options.length; i++) {
                                                if ((sel.options[i].text || '').includes('Mr')) {
                                                    sel.selectedIndex = i;
                                                    sel.dispatchEvent(new Event('change', { bubbles: true }));
                                                    break;
                                                }
                                            }
                                        }
                                    }");
                                }

                                // 2. Click title trigger if custom dropdown
                                var titleTrigger = f.Locator("[aria-label*='title' i], button:has-text('title'), div:has-text('What’s your title'), div:has-text('What\'s your title'), span:has-text('What\'s your title')").Last;
                                if (await titleTrigger.CountAsync() > 0 && await titleTrigger.IsVisibleAsync())
                                {
                                    await titleTrigger.ClickAsync(new() { Force = true });
                                    await Task.Delay(800);
                                }

                                // 3. Select Mr option
                                var mrOpt = f.Locator("li:has-text('Mr'), [role='option']:has-text('Mr'), button:has-text('Mr')").First;
                                if (await mrOpt.CountAsync() > 0 && await mrOpt.IsVisibleAsync())
                                {
                                    await mrOpt.ClickAsync(new() { Force = true });
                                    await Task.Delay(600);
                                }

                                // 4. DOM fallback for Mr option
                                await f.EvaluateAsync(@"() => {
                                    const all = Array.from(document.querySelectorAll('*'));
                                    const mrLeaf = all.find(el => {
                                        const t = (el.innerText || el.textContent || '').trim();
                                        return (t === 'Mr.' || t === 'Mr' || t.startsWith('Mr.\n') || t.startsWith('Mr\n')) && el.children.length === 0;
                                    });
                                    if (mrLeaf) {
                                        let clickableOpt = mrLeaf;
                                        while (clickableOpt && clickableOpt.tagName !== 'BODY') {
                                            if (clickableOpt.tagName === 'LI' || clickableOpt.getAttribute('role') === 'option' || clickableOpt.tagName === 'BUTTON') {
                                                break;
                                            }
                                            if (clickableOpt.parentElement && clickableOpt.parentElement.tagName !== 'BODY') clickableOpt = clickableOpt.parentElement;
                                            else break;
                                        }
                                        clickableOpt.click();
                                    }
                                }");
                            }
                            catch { }
                        }

                        // B. Check terms agreement checkbox across all frames
                        foreach (var f in paypalPage.Frames)
                        {
                            try
                            {
                                var cb = f.Locator("input[type='checkbox']").First;
                                if (await cb.CountAsync() > 0 && !await cb.IsCheckedAsync())
                                {
                                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Checking agreement checkbox via CheckAsync in frame {f.Url}");
                                    await cb.CheckAsync(new() { Force = true });
                                    await Task.Delay(300);
                                }

                                var label = f.Locator("label:has-text('Agreement'), div:has-text('Agreement'), span:has-text('Agreement')").First;
                                if (await label.CountAsync() > 0 && await label.IsVisibleAsync())
                                {
                                    await label.ClickAsync(new() { Force = true });
                                    await Task.Delay(300);
                                }

                                // DOM evaluate to ensure checked
                                await f.EvaluateAsync(@"() => {
                                    const cb = document.querySelector('input[type=""checkbox""]');
                                    if (cb && !cb.checked) {
                                        cb.click();
                                        cb.checked = true;
                                        cb.dispatchEvent(new Event('input', { bubbles: true }));
                                        cb.dispatchEvent(new Event('change', { bubbles: true }));
                                    }
                                }");
                            }
                            catch { }
                        }

                        // B2. If Autopay requires adding a debit card ("To setup autopay, add a debit card")
                        var addCardClicked = false;
                        foreach (var f in paypalPage.Frames)
                        {
                            try
                            {
                                var addCardBtn = f.Locator("button:has-text('Add a debit card'), [role='button']:has-text('Add a debit card'), a:has-text('add a debit card'), div:has-text('Add a debit card')").Last;
                                if (await addCardBtn.CountAsync() > 0 && await addCardBtn.IsVisibleAsync())
                                {
                                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Found 'Add a debit card' button for Autopay in frame {f.Url}, clicking...");
                                    lastActionTime = DateTime.UtcNow;
                                    await addCardBtn.ClickAsync(new() { Force = true });
                                    await Task.Delay(3000);
                                    addCardClicked = true;
                                    break;
                                }
                            }
                            catch (Exception exCard)
                            {
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Autopay card setup note: {exCard.Message}");
                            }
                        }
                        if (addCardClicked)
                        {
                            continue;
                        }

                        await Task.Delay(600);

                        // C. Click Agree and Continue across all frames
                        var clickedAgree = false;
                        foreach (var f in paypalPage.Frames)
                        {
                            var agreeBtn = f.Locator("button:has-text('Agree and Continue'), [role='button']:has-text('Agree and Continue'), button:has-text('Agree & Continue'), [role='button']:has-text('Agree & Continue')").Last;
                            if (await agreeBtn.CountAsync() > 0 && await agreeBtn.IsVisibleAsync())
                            {
                                await agreeBtn.ScrollIntoViewIfNeededAsync();
                                var btnDisabled = await agreeBtn.GetAttributeAsync("disabled");
                                var ariaDisabled = await agreeBtn.GetAttributeAsync("aria-disabled");
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Agree button found in frame {f.Url}: disabled='{btnDisabled}', aria-disabled='{ariaDisabled}'");

                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking 'Agree and Continue' button");
                                try
                                {
                                    await agreeBtn.ClickAsync(new() { Force = true, Timeout = 5000 });
                                }
                                catch
                                {
                                    await f.EvaluateAsync(@"() => {
                                        const btn = Array.from(document.querySelectorAll('button, [role=""button""], a')).find(e => 
                                            e.innerText && (e.innerText.includes('Agree and Continue') || e.innerText.includes('Agree & Continue'))
                                        );
                                        if (btn) btn.click();
                                    }");
                                }

                                lastActionTime = DateTime.UtcNow;
                                clickedAgree = true;
                                break;
                            }
                        }

                        if (!clickedAgree)
                        {
                            foreach (var f in paypalPage.Frames)
                            {
                                try
                                {
                                    var domClicked = await f.EvaluateAsync<bool>(@"() => {
                                        const btn = Array.from(document.querySelectorAll('button, [role=""button""], a')).find(e => 
                                            e.innerText && (e.innerText.includes('Agree and Continue') || e.innerText.includes('Agree & Continue'))
                                        );
                                        if (btn) { btn.click(); return true; }
                                        return false;
                                    }");
                                    if (domClicked)
                                    {
                                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicked Agree and Continue via DOM evaluate in frame {f.Url}");
                                        lastActionTime = DateTime.UtcNow;
                                        clickedAgree = true;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }

                        if (clickedAgree)
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for PayPal popup to close or complete after agreement...");
                            var closeWaitStart = DateTime.UtcNow;
                            while (!paypalPage.IsClosed && DateTime.UtcNow - closeWaitStart < TimeSpan.FromSeconds(15))
                            {
                                if (page.Url.Contains("/checkout/completed") || page.Url.Contains("ConfirmOrder"))
                                    break;
                                await Task.Delay(1000);
                            }

                            if (paypalPage.IsClosed)
                            {
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] PayPal popup closed successfully!");
                                break;
                            }
                        }
                    }
                    // Case 2: Phone number / address update screen (e.g. "Billing address and phone number", "Enter a valid phone number")
                    else if (screenText.Contains("Billing address and phone number") || screenText.Contains("Enter a valid phone number") || (screenText.Contains("Phone number") && await targetFrame.Locator("button:has-text('Save')").CountAsync() > 0))
                    {
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] On Phone / Address fix screen, updating phone number");
                        try
                        {
                            var phoneInput = targetFrame.Locator("input[name*='phone' i], input[id*='phone' i], input[type='tel']").First;
                            if (await phoneInput.CountAsync() > 0 && await phoneInput.IsVisibleAsync())
                            {
                                await phoneInput.ClickAsync();
                                await phoneInput.PressAsync("ControlOrMeta+A");
                                await phoneInput.PressAsync("Backspace");
                                await Task.Delay(200);

                                await targetFrame.EvaluateAsync(@"() => {
                                    const input = document.querySelector('input[name*=""phone"" i], input[id*=""phone"" i], input[type=""tel""]');
                                    if (input) {
                                        input.value = '';
                                        input.dispatchEvent(new Event('input', { bubbles: true }));
                                    }
                                }");
                                await Task.Delay(200);

                                await phoneInput.PressSequentiallyAsync("07123456789", new() { Delay = 40 });
                                await Task.Delay(500);

                                var currentVal = await phoneInput.InputValueAsync();
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Updated phone number input value: '{currentVal}'");
                            }

                            var saveBtn = targetFrame.Locator("button:has-text('Save')").First;
                            if (await saveBtn.CountAsync() > 0 && await saveBtn.IsVisibleAsync())
                            {
                                lastActionTime = DateTime.UtcNow;
                                await saveBtn.ClickAsync();
                                await Task.Delay(3500);
                                continue;
                            }
                        }
                        catch (Exception exPhone)
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Phone fix note: {exPhone.Message}");
                        }
                    }
                    // Case 2B: Link a card screen ("Link a card")
                    else if (screenText.Contains("Link a card") || (screenText.Contains("Card number") && await targetFrame.Locator("button:has-text('Link Card'), button:has-text('Link')").CountAsync() > 0))
                    {
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] On 'Link a card' screen, checking for existing card or filling card inputs");
                        lastActionTime = DateTime.UtcNow;

                        try
                        {
                            // 0. If an existing card option is present (e.g. Mastercard Credit ****6383), select it
                            var existingCard = targetFrame.Locator("label:has-text('Mastercard'), label:has-text('Visa'), div:has-text('Mastercard'), div:has-text('Visa'), input[type='radio'][value*='6383']").First;
                            if (await existingCard.CountAsync() == 0 || !await existingCard.IsVisibleAsync())
                                existingCard = paypalPage.Locator("label:has-text('Mastercard'), label:has-text('Visa'), div:has-text('Mastercard'), div:has-text('Visa'), input[type='radio'][value*='6383']").First;

                            if (await existingCard.CountAsync() > 0 && await existingCard.IsVisibleAsync())
                            {
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Existing card found on Link a Card screen, selecting existing card...");
                                await existingCard.ClickAsync(new() { Force = true });
                                await Task.Delay(1500);

                                var continueBtn = targetFrame.Locator("button:has-text('Continue'), [role='button']:has-text('Continue'), button:has-text('Next'), #payment-submit-btn").Last;
                                if (await continueBtn.CountAsync() == 0 || !await continueBtn.IsVisibleAsync())
                                    continueBtn = paypalPage.Locator("button:has-text('Continue'), [role='button']:has-text('Continue'), button:has-text('Next'), #payment-submit-btn").Last;

                                if (await continueBtn.CountAsync() > 0 && await continueBtn.IsVisibleAsync())
                                {
                                    await continueBtn.ClickAsync(new() { Force = true });
                                    await Task.Delay(3500);
                                    continue;
                                }
                            }

                            // 1. Fill card inputs across all frames
                            foreach (var f in paypalPage.Frames)
                            {
                                try
                                {
                                    var cardInp = f.Locator("input[autocomplete*='cc-number'], input[placeholder*='Card number' i], input[name*='cardNumber' i], input[id*='cardNumber' i], input[aria-label*='card number' i]").First;
                                    if (await cardInp.CountAsync() == 0 || !await cardInp.IsVisibleAsync())
                                        cardInp = f.Locator("input[name*='card' i]:not([name*='exp' i]):not([name*='cvv' i]):not([name*='date' i])").First;

                                    if (await cardInp.CountAsync() > 0 && await cardInp.IsVisibleAsync())
                                    {
                                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Found Card Number input in frame {f.Url}, typing 4111111111111111");
                                        await cardInp.ClickAsync();
                                        await cardInp.PressAsync("ControlOrMeta+A");
                                        await cardInp.PressAsync("Backspace");
                                        await cardInp.PressSequentiallyAsync("4111111111111111", new() { Delay = 50 });
                                        await Task.Delay(300);
                                    }

                                    var expInp = f.Locator("input[autocomplete*='cc-exp'], input[placeholder*='Expiry' i], input[placeholder*='MM' i], input[name*='exp' i], input[id*='exp' i], input[aria-label*='exp' i]").First;
                                    if (await expInp.CountAsync() > 0 && await expInp.IsVisibleAsync())
                                    {
                                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Found Expiry input in frame {f.Url}, typing 1228");
                                        await expInp.ClickAsync();
                                        await expInp.PressAsync("ControlOrMeta+A");
                                        await expInp.PressAsync("Backspace");
                                        await expInp.PressSequentiallyAsync("1228", new() { Delay = 50 });
                                        await Task.Delay(300);
                                    }

                                    var cvvInp = f.Locator("input[autocomplete*='csc'], input[placeholder*='CVV' i], input[placeholder*='CVC' i], input[name*='cvv' i], input[id*='cvv' i], input[name*='cvc' i], input[id*='cvc' i], input[name*='security' i], input[id*='security' i], input[aria-label*='cvv' i], input[aria-label*='cvc' i]").First;
                                    if (await cvvInp.CountAsync() == 0 || !await cvvInp.IsVisibleAsync())
                                        cvvInp = f.GetByLabel("CVV").First;
                                    if (await cvvInp.CountAsync() == 0 || !await cvvInp.IsVisibleAsync())
                                        cvvInp = f.Locator("label:has-text('CVV') ~ input, div:has-text('CVV') > input, input[name*='code' i]").First;

                                    if (await cvvInp.CountAsync() > 0 && await cvvInp.IsVisibleAsync())
                                    {
                                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Found CVV input in frame {f.Url}, typing 123");
                                        await cvvInp.ClickAsync();
                                        await cvvInp.PressAsync("ControlOrMeta+A");
                                        await cvvInp.PressAsync("Backspace");
                                        await cvvInp.PressSequentiallyAsync("123", new() { Delay = 50 });
                                        await Task.Delay(300);
                                    }
                                }
                                catch (Exception exF)
                                {
                                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Frame input filling note ({f.Url}): {exF.Message}");
                                }
                            }

                            // 2. Also run DOM evaluate across all frames to ensure inputs are populated and events dispatched
                            foreach (var f in paypalPage.Frames)
                            {
                                try
                                {
                                    await f.EvaluateAsync(@"() => {
                                        const inputs = Array.from(document.querySelectorAll('input:not([type=""hidden""]):not([type=""checkbox""]):not([type=""radio""])')).filter(i => i.offsetParent !== null);
                                        for (const inp of inputs) {
                                            const label = ((inp.getAttribute('aria-label') || '') + ' ' + (inp.placeholder || '') + ' ' + (inp.name || '') + ' ' + (inp.id || '')).toLowerCase();
                                            const isExp = label.includes('exp') || label.includes('mm') || label.includes('expiry');
                                            const isCvv = label.includes('cvv') || label.includes('cvc') || label.includes('security') || label.includes('csc');
                                            const isCard = (label.includes('cardnumber') || label.includes('card-number') || label.includes('cc-number') || (label.includes('card') && !isExp && !isCvv) || label.includes('number')) && !label.includes('phone') && !isExp && !isCvv;

                                            if (isCard && !inp.value) {
                                                inp.focus();
                                                inp.value = '4111111111111111';
                                                inp.dispatchEvent(new Event('input', { bubbles: true }));
                                                inp.dispatchEvent(new Event('change', { bubbles: true }));
                                                inp.blur();
                                            } else if (isExp && (!inp.value || inp.value.includes('41'))) {
                                                inp.focus();
                                                inp.value = '12/28';
                                                inp.dispatchEvent(new Event('input', { bubbles: true }));
                                                inp.dispatchEvent(new Event('change', { bubbles: true }));
                                                inp.blur();
                                            } else if (isCvv && !inp.value) {
                                                inp.focus();
                                                inp.value = '123';
                                                inp.dispatchEvent(new Event('input', { bubbles: true }));
                                                inp.dispatchEvent(new Event('change', { bubbles: true }));
                                                inp.blur();
                                            }
                                        }

                                        // Fallback 1: If 3 or more text inputs exist and first has the test card, the 3rd is CVV
                                        if (inputs.length >= 3 && inputs.some(i => (i.value || '').replace(/\s/g, '').includes('4111111111111111'))) {
                                            const cvv = inputs[2];
                                            if (!cvv.value) {
                                                cvv.focus();
                                                cvv.value = '123';
                                                cvv.dispatchEvent(new Event('input', { bubbles: true }));
                                                cvv.dispatchEvent(new Event('change', { bubbles: true }));
                                                cvv.blur();
                                            }
                                        }

                                        // Fallback 2: Look for label/element with text 'CVV'
                                        const allEls = Array.from(document.querySelectorAll('label, div, span, p'));
                                        const cvvEl = allEls.find(l => (l.innerText || '').trim().toUpperCase() === 'CVV');
                                        if (cvvEl) {
                                            let forId = cvvEl.getAttribute('for');
                                            let targetInp = forId ? document.getElementById(forId) : null;
                                            if (!targetInp) {
                                                let p = cvvEl.parentElement;
                                                for (let d = 0; d < 3 && p; d++) {
                                                    targetInp = p.querySelector('input');
                                                    if (targetInp) break;
                                                    p = p.parentElement;
                                                }
                                            }
                                            if (targetInp && !targetInp.value) {
                                                targetInp.focus();
                                                targetInp.value = '123';
                                                targetInp.dispatchEvent(new Event('input', { bubbles: true }));
                                                targetInp.dispatchEvent(new Event('change', { bubbles: true }));
                                                targetInp.blur();
                                            }
                                        }
                                    }");
                                }
                                catch { }
                            }

                            await Task.Delay(500);

                            // 3. Click 'Link Card' button
                            var linkBtn = targetFrame.Locator("button:has-text('Link Card'), button:has-text('Link card'), button:has-text('Link'), [role='button']:has-text('Link Card')").Last;
                            if (await linkBtn.CountAsync() == 0 || !await linkBtn.IsVisibleAsync())
                                linkBtn = paypalPage.Locator("button:has-text('Link Card'), button:has-text('Link card'), button:has-text('Link'), [role='button']:has-text('Link Card')").Last;

                            if (await linkBtn.CountAsync() > 0 && await linkBtn.IsVisibleAsync())
                            {
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking 'Link Card' button");
                                lastActionTime = DateTime.UtcNow;
                                await linkBtn.ClickAsync(new() { Force = true });
                                await Task.Delay(5000);
                                continue;
                            }
                            else
                            {
                                // Try DOM click on Link Card button in all frames
                                foreach (var f in paypalPage.Frames)
                                {
                                    try
                                    {
                                        var clicked = await f.EvaluateAsync<bool>(@"() => {
                                            const btn = Array.from(document.querySelectorAll('button, [role=""button""]')).find(b => (b.innerText || '').toLowerCase().includes('link'));
                                            if (btn) { btn.click(); return true; }
                                            return false;
                                        }");
                                        if (clicked)
                                        {
                                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicked Link Card button via DOM evaluate in frame {f.Url}");
                                            lastActionTime = DateTime.UtcNow;
                                            await Task.Delay(5000);
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                                continue;
                            }
                        }
                        catch (Exception exLink)
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Link card note: {exLink.Message}");
                        }
                    }
                    // Case 3: Loan overview screen (Screen 2: "Loan of £... due ...")
                    else if (screenText.Contains("Loan of") || screenText.Contains("due today") || screenText.Contains("Interest-free loan"))
                    {
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] On Pay Later loan overview screen, clicking Continue");
                        var continueBtn = targetFrame.Locator("button:has-text('Continue'), [role='button']:has-text('Continue')").Last;
                        if (await continueBtn.CountAsync() == 0 || !await continueBtn.IsVisibleAsync())
                            continueBtn = paypalPage.Locator("button:has-text('Continue'), [role='button']:has-text('Continue')").Last;

                        if (await continueBtn.CountAsync() > 0 && await continueBtn.IsVisibleAsync())
                        {
                            lastActionTime = DateTime.UtcNow;
                            await continueBtn.ClickAsync(new() { Force = true });
                            await Task.Delay(3500);
                            continue;
                        }
                    }
                    // Case 3B: Pay Later plan selection screen ("Pay in 30 Days", "Pay in 3", "Pay Later")
                    else if (screenText.Contains("Pay in 30 Days") || screenText.Contains("Pay Later") || screenText.Contains("Pay in 3"))
                    {
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] On Pay Later plan selection screen");

                        // Check if a backup card / payment method under 'Pay with' needs selection
                        try
                        {
                            var cardOption = targetFrame.Locator("label:has-text('Mastercard'), label:has-text('Visa'), label:has-text('Debit'), label:has-text('Credit')").First;
                            if (await cardOption.CountAsync() == 0 || !await cardOption.IsVisibleAsync())
                                cardOption = paypalPage.Locator("label:has-text('Mastercard'), label:has-text('Visa'), label:has-text('Debit'), label:has-text('Credit')").First;

                            if (await cardOption.CountAsync() > 0 && await cardOption.IsVisibleAsync())
                            {
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Selecting backup card for Pay Later");
                                await cardOption.ClickAsync(new() { Force = true });
                                await Task.Delay(500);
                            }
                        }
                        catch { }

                        var planContinueBtn = targetFrame.Locator("button:has-text('Continue'), [role='button']:has-text('Continue'), #payment-submit-btn, button[name='payment-submit-btn']").Last;
                        if (await planContinueBtn.CountAsync() == 0 || !await planContinueBtn.IsVisibleAsync())
                            planContinueBtn = paypalPage.Locator("button:has-text('Continue'), [role='button']:has-text('Continue'), #payment-submit-btn, button[name='payment-submit-btn']").Last;

                        if (await planContinueBtn.CountAsync() > 0 && await planContinueBtn.IsVisibleAsync())
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking Continue on Pay Later plan selection screen via Playwright");
                            lastActionTime = DateTime.UtcNow;
                            await planContinueBtn.ClickAsync(new() { Force = true });
                            await Task.Delay(4000);
                            continue;
                        }
                        else
                        {
                            // DOM click across all frames
                            var clickedPlan = false;
                            foreach (var f in paypalPage.Frames)
                            {
                                try
                                {
                                    clickedPlan = await f.EvaluateAsync<bool>(@"() => {
                                        const btn = Array.from(document.querySelectorAll('button, [role=""button""]')).find(b => {
                                            const t = (b.innerText || '').trim();
                                            return t === 'Continue' || t.startsWith('Continue');
                                        });
                                        if (btn) { btn.click(); return true; }
                                        return false;
                                    }");
                                    if (clickedPlan)
                                    {
                                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicked Continue via DOM evaluate in frame {f.Url}");
                                        lastActionTime = DateTime.UtcNow;
                                        await Task.Delay(4000);
                                        break;
                                    }
                                }
                                catch { }
                            }
                            continue;
                        }
                    }
                    // Case 4: Initial review or general step
                    else
                    {
                        var generalContinueBtn = targetFrame.Locator("button:has-text('Continue'):not(:has-text('Agree')), [role='button']:has-text('Continue'):not(:has-text('Agree')), #payment-submit-btn").Last;
                        if (await generalContinueBtn.CountAsync() == 0 || !await generalContinueBtn.IsVisibleAsync())
                            generalContinueBtn = paypalPage.Locator("button:has-text('Continue'):not(:has-text('Agree')), [role='button']:has-text('Continue'):not(:has-text('Agree')), #payment-submit-btn").Last;

                        var payBtn = targetFrame.Locator("button:has-text('Pay'), button:has-text('Pay Now'), button:has-text('Complete Purchase')").Last;
                        if (await payBtn.CountAsync() == 0 || !await payBtn.IsVisibleAsync())
                            payBtn = paypalPage.Locator("button:has-text('Pay'), button:has-text('Pay Now'), button:has-text('Complete Purchase')").Last;

                        if (await generalContinueBtn.CountAsync() > 0 && await generalContinueBtn.IsVisibleAsync())
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking general 'Continue' button");
                            lastActionTime = DateTime.UtcNow;
                            await generalContinueBtn.ClickAsync(new() { Force = true });
                            await Task.Delay(3500);
                        }
                        else if (await payBtn.CountAsync() > 0 && await payBtn.IsVisibleAsync())
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking 'Pay' button");
                            lastActionTime = DateTime.UtcNow;
                            await payBtn.ClickAsync(new() { Force = true });
                            await Task.Delay(3500);
                        }
                        else
                        {
                            var clickedAny = false;
                            foreach (var f in paypalPage.Frames)
                            {
                                try
                                {
                                    clickedAny = await f.EvaluateAsync<bool>(@"() => {
                                        const btn = Array.from(document.querySelectorAll('button, [role=""button""]')).find(b => {
                                            const t = (b.innerText || '').trim().toLowerCase();
                                            return t === 'continue' || t === 'pay' || t === 'pay now' || t === 'complete purchase';
                                        });
                                        if (btn) { btn.click(); return true; }
                                        return false;
                                    }");
                                    if (clickedAny)
                                    {
                                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicked general submit button via DOM evaluate in frame {f.Url}");
                                        lastActionTime = DateTime.UtcNow;
                                        await Task.Delay(3500);
                                        break;
                                    }
                                }
                                catch { }
                            }
                            if (!clickedAny)
                            {
                                await Task.Delay(1500);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Multi-step loop check: {ex.Message}");
                    await Task.Delay(1500);
                }
            }
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

    private async Task EnsurePayLaterProductInCartAsync(IPage page)
    {
        try
        {
            var slug = !string.IsNullOrWhiteSpace(Settings.PayPal?.PayLaterProductSlug)
                ? Settings.PayPal.PayLaterProductSlug
                : "chamomile-and-calendula-face-and-body-balm";

            var productUrl = $"{Settings.BaseUrl.TrimEnd('/')}/{slug.TrimStart('/')}";
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Navigating to product page to meet £30 Pay Later minimum threshold: {productUrl}");
            await page.GotoAsync(productUrl, new() { WaitUntil = WaitUntilState.Load });
            await DismissCookieNotificationAsync(page);

            // If product has attribute dropdowns (e.g. size), prefer Large (140) for £15.00
            var attrSelects = page.Locator("#product_attribute_40, select[data-attr='40'], select[name*='product_attribute_'], select[data-attr]");
            var selectCount = await attrSelects.CountAsync();
            for (var i = 0; i < selectCount; i++)
            {
                var select = attrSelects.Nth(i);
                if (await select.IsVisibleAsync())
                {
                    var options = await select.Locator("option").AllAsync();
                    string? chosenVal = null;
                    foreach (var opt in options)
                    {
                        var val = await opt.GetAttributeAsync("value");
                        var text = await opt.InnerTextAsync();
                        if (val == "140" || text.Contains("Large", StringComparison.OrdinalIgnoreCase))
                        {
                            chosenVal = val;
                            break;
                        }
                        if (chosenVal == null && !string.IsNullOrWhiteSpace(val) && val != "0")
                        {
                            chosenVal = val;
                        }
                    }
                    if (chosenVal != null)
                    {
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Selecting product attribute value: {chosenVal}");
                        await select.SelectOptionAsync(chosenVal);
                        await Task.Delay(1500);
                    }
                }
            }

            // Click Add to Cart button (first item)
            var addToCartButton = page.Locator("#add-to-cart-button-187, button[id^='add-to-cart-button']:not(.coming-soon-button), .add-to-cart-button:not(.coming-soon-button)").First;
            await addToCartButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Clicking Add to Cart button (first item)");

            var responseTask = page.WaitForResponseAsync(
                r => r.Url.Contains("/addproducttocart/"),
                new() { Timeout = 10000 });

            await addToCartButton.ClickAsync(new() { Force = true });

            try
            {
                var resp = await responseTask;
                var json = await resp.TextAsync();
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] AddToCart (1) response: {json}");
            }
            catch
            {
                await Task.Delay(2500);
            }

            // Click Add to Cart a second time so 2 x £15.00 = £30.00 threshold
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Clicking Add to Cart button (second item to reach £30 threshold)");
            try
            {
                var secondResponseTask = page.WaitForResponseAsync(
                    r => r.Url.Contains("/addproducttocart/"),
                    new() { Timeout = 10000 });

                await addToCartButton.ClickAsync(new() { Force = true });
                var resp2 = await secondResponseTask;
                var json2 = await resp2.TextAsync();
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] AddToCart (2) response: {json2}");
            }
            catch
            {
                await Task.Delay(2500);
            }

            var notification = page.Locator("#bar-notification .bar-notification.success");
            try
            {
                await notification.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            }
            catch { }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Failed to add Pay Later product: {ex.Message}. Falling back to high quantity test product.");
            await AddTestProductToCartWithHighQuantityAsync(page);
        }
    }

    private async Task AddTestProductToCartWithHighQuantityAsync(IPage page)
    {
        try
        {
            var productUrl = $"{Settings.BaseUrl.TrimEnd('/')}/{Settings.TestProductSlug.TrimStart('/')}";
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Adding test product '{Settings.TestProductSlug}' with high quantity to satisfy £30 Pay Later threshold: {productUrl}");
            await page.GotoAsync(productUrl, new() { WaitUntil = WaitUntilState.Load });
            await DismissCookieNotificationAsync(page);

            // Set quantity to 3500 (at £0.01 = £35.00, satisfying £30 threshold)
            var qtyInput = page.Locator("input.qty-input, input[id^='product_enteredQuantity']").First;
            if (await qtyInput.CountAsync() > 0 && await qtyInput.IsVisibleAsync())
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Setting product quantity to 3500");
                await qtyInput.FillAsync("3500");
                await Task.Delay(500);
            }

            var addToCartButton = page.Locator(".add-to-cart-button, button[id^='add-to-cart-button']").First;
            await addToCartButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] Clicking Add to Cart button");

            var responseTask = page.WaitForResponseAsync(
                r => r.Url.Contains("/addproducttocart/") && r.Status == 200,
                new() { Timeout = 10000 });

            await addToCartButton.ClickAsync(new() { Force = true });

            try
            {
                await responseTask;
            }
            catch
            {
                await Task.Delay(2500);
            }

            var notification = page.Locator("#bar-notification .bar-notification.success");
            try
            {
                await notification.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            }
            catch { }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [PayLater] AddTestProductToCartWithHighQuantityAsync failed: {ex.Message}");
        }
    }
}
