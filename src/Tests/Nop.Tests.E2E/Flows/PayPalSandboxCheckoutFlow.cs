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

            // 3. Locate PayPal Smart Button based on funding source
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for PayPal Smart Button iframe");
            var paypalFrame = page.FrameLocator("iframe[name^='__zoid__paypal_buttons'], .paypal-button-container iframe.component-frame, iframe[title='PayPal']");

            ILocator paypalButton = FundingSource switch
            {
                PayPalFundingSource.PayLater => paypalFrame.Locator("[data-funding-source='paylater']").First,
                PayPalFundingSource.Card => paypalFrame.Locator("[data-funding-source='card']").First,
                _ => paypalFrame.Locator("[data-funding-source='paypal'], .paypal-button").First
            };

            await paypalButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 25000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] PayPal {FundingSource} button visible, preparing popup listener and clicking button");

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
                submitBtn = paypalPage.Locator("button:has-text('Complete Purchase'), button:has-text('Pay Now'), button:has-text('Agree and Continue'), button:has-text('Agree & Continue'), button:has-text('Save and Continue'), button:has-text('Continue'), #payment-submit-btn, button[name='payment-submit-btn'], [data-testid='submit-button-initial']").Last;
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
            await submitBtn.ClickAsync();

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
                                    fText.Contains("Link a card") || fText.Contains("Choose a way to pay") || fText.Contains("Pay in 3"))
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
                    if (screenText.Contains("Confirm your details") || screenText.Contains("Agree and Continue") || screenText.Contains("What's your title"))
                    {
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] On Pay Later 'Confirm your details' screen");

                        // A. Fill Title dropdown if needed
                        try
                        {
                            var titleResult = await targetFrame.EvaluateAsync<string>(@"() => {
                                // 1. Check if native select exists
                                const sel = document.querySelector('select');
                                if (sel && sel.options.length > 1) {
                                    for (let i = 0; i < sel.options.length; i++) {
                                        if ((sel.options[i].text || '').includes('Mr')) {
                                            sel.selectedIndex = i;
                                            sel.dispatchEvent(new Event('change', { bubbles: true }));
                                            return 'Native select updated to ' + sel.options[i].text;
                                        }
                                    }
                                }

                                // 2. Find any element containing 'title'
                                const all = Array.from(document.querySelectorAll('*'));
                                const leaf = all.find(el => {
                                    const text = (el.innerText || el.textContent || '').trim().toLowerCase();
                                    return text.includes('title') && el.children.length === 0;
                                });

                                if (!leaf) return 'No leaf containing title found';

                                // Find clickable element by walking up
                                let clickable = leaf;
                                while (clickable && clickable.tagName !== 'BODY') {
                                    if (clickable.tagName === 'BUTTON' || clickable.getAttribute('role') === 'button' || clickable.getAttribute('aria-haspopup') || clickable.onclick) {
                                        break;
                                    }
                                    if (clickable.parentElement && clickable.parentElement.tagName !== 'BODY') {
                                        clickable = clickable.parentElement;
                                    } else {
                                        break;
                                    }
                                }

                                if (clickable) {
                                    clickable.scrollIntoView({ block: 'center' });
                                    clickable.click();
                                    return 'Clicked title element: ' + clickable.tagName + ' (id=' + clickable.id + ', class=' + clickable.className + ')';
                                }
                                return 'No clickable ancestor found for title';
                            }");

                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Title click attempt: {titleResult}");
                            await Task.Delay(1000);

                            // Select Mr. option from the opened dropdown / bottom sheet
                            var selectMrResult = await targetFrame.EvaluateAsync<string>(@"() => {
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
                                        if (clickableOpt.parentElement && clickableOpt.parentElement.tagName !== 'BODY') {
                                            clickableOpt = clickableOpt.parentElement;
                                        } else {
                                            break;
                                        }
                                    }
                                    clickableOpt.click();
                                    return 'Clicked Mr option: ' + clickableOpt.tagName;
                                }
                                return 'No Mr option found';
                            }");

                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Title option selection result: {selectMrResult}");
                            await Task.Delay(800);

                            // Dismiss modal if still open
                            try
                            {
                                await targetFrame.EvaluateAsync(@"() => {
                                    const closeBtn = document.querySelector('button[aria-label*=""close"" i], button[aria-label*=""dismiss"" i]');
                                    if (closeBtn && closeBtn.offsetParent !== null) closeBtn.click();
                                }");
                            }
                            catch { }
                        }
                        catch (Exception exTitle)
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Title selection note: {exTitle.Message}");
                        }

                        // B. Check terms agreement checkbox
                        try
                        {
                            var cb = targetFrame.Locator("input[type='checkbox']").First;
                            if (await cb.CountAsync() > 0)
                            {
                                var isChecked = await cb.IsCheckedAsync();
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] input[type='checkbox'] found, checked={isChecked}");
                                if (!isChecked)
                                {
                                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Checking agreement checkbox via Playwright CheckAsync...");
                                    await cb.CheckAsync(new() { Force = true });
                                    await Task.Delay(400);
                                }
                            }
                            else
                            {
                                var label = targetFrame.Locator("label:has-text('Pay in 30 Days Agreement'), div:has-text('Pay in 30 Days Agreement')").First;
                                if (await label.CountAsync() > 0)
                                {
                                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking agreement label");
                                    await label.ClickAsync(new() { Force = true });
                                    await Task.Delay(400);
                                }
                            }

                            // Verify state via evaluate and ensure it stays checked
                            var isNowChecked = await targetFrame.EvaluateAsync<bool>(@"() => {
                                const cb = document.querySelector('input[type=""checkbox""]');
                                if (cb) {
                                    if (!cb.checked) {
                                        cb.click();
                                    }
                                    return cb.checked;
                                }
                                return false;
                            }");
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Agreement checkbox final state: checked={isNowChecked}");
                        }
                        catch (Exception exCb)
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Checkbox check note: {exCb.Message}");
                        }

                        // B2. If Autopay requires adding a debit card ("To setup autopay, add a debit card")
                        try
                        {
                            var addCardBtn = targetFrame.Locator("button:has-text('Add a debit card'), [role='button']:has-text('Add a debit card'), div:has-text('Add a debit card'), a:has-text('add a debit card')").Last;
                            if (await addCardBtn.CountAsync() > 0 && await addCardBtn.IsVisibleAsync())
                            {
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Found 'Add a debit card' button for Autopay, navigating to Link a card screen...");
                                lastActionTime = DateTime.UtcNow;
                                await addCardBtn.ClickAsync(new() { Force = true });
                                await Task.Delay(2500);
                                continue;
                            }
                        }
                        catch (Exception exCard)
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Autopay card setup note: {exCard.Message}");
                        }

                        await Task.Delay(800);

                        // C. Click Agree and Continue
                        var agreeBtn = targetFrame.Locator("button:has-text('Agree and Continue'), [role='button']:has-text('Agree and Continue'), button:has-text('Agree & Continue'), [role='button']:has-text('Agree & Continue')").Last;
                        if (await agreeBtn.CountAsync() == 0)
                        {
                            agreeBtn = targetFrame.GetByText("Agree and Continue").Last;
                        }
                        if (await agreeBtn.CountAsync() > 0)
                        {
                            await agreeBtn.ScrollIntoViewIfNeededAsync();
                            var btnDisabled = await agreeBtn.GetAttributeAsync("disabled");
                            var ariaDisabled = await agreeBtn.GetAttributeAsync("aria-disabled");
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Agree button found: disabled='{btnDisabled}', aria-disabled='{ariaDisabled}'");

                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking 'Agree and Continue' button");
                            try
                            {
                                await agreeBtn.ClickAsync(new() { Force = true, Timeout = 5000 });
                            }
                            catch (Exception exClick)
                            {
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Playwright click note: {exClick.Message}, clicking via DOM");
                                await targetFrame.EvaluateAsync(@"() => {
                                    const btn = Array.from(document.querySelectorAll('button, [role=""button""], a')).find(e => 
                                        e.innerText && (e.innerText.includes('Agree and Continue') || e.innerText.includes('Agree & Continue'))
                                    );
                                    if (btn) btn.click();
                                }");
                            }

                            lastActionTime = DateTime.UtcNow;

                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for PayPal popup to close or complete after agreement...");
                            var closeWaitStart = DateTime.UtcNow;
                            while (!paypalPage.IsClosed && DateTime.UtcNow - closeWaitStart < TimeSpan.FromSeconds(10))
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
                        else
                        {
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Agree button not found, dumping frame buttons:");
                            var buttons = await targetFrame.EvaluateAsync<string>(@"() => {
                                return Array.from(document.querySelectorAll('button, [role=""button""], a')).map(b => b.innerText || b.getAttribute('aria-label') || b.id).join(' | ');
                            }");
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Buttons in frame: {buttons}");
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
                        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] On 'Link a card' screen, filling card inputs");
                        try
                        {
                            var cardInputs = targetFrame.Locator("input:not([type='hidden']):not([type='checkbox']):not([type='radio'])");
                            var count = await cardInputs.CountAsync();
                            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Found {count} inputs on Link a card screen");

                            if (count >= 3)
                            {
                                var cardNum = cardInputs.Nth(0);
                                await cardNum.ClickAsync();
                                await cardNum.PressAsync("ControlOrMeta+A");
                                await cardNum.PressAsync("Backspace");
                                await cardNum.PressSequentiallyAsync("4111111111111111", new() { Delay = 30 });
                                await Task.Delay(400);

                                var exp = cardInputs.Nth(1);
                                await exp.ClickAsync();
                                await exp.PressAsync("ControlOrMeta+A");
                                await exp.PressAsync("Backspace");
                                await exp.PressSequentiallyAsync("1228", new() { Delay = 30 });
                                await Task.Delay(400);

                                var cvv = cardInputs.Nth(2);
                                await cvv.ClickAsync();
                                await cvv.PressAsync("ControlOrMeta+A");
                                await cvv.PressAsync("Backspace");
                                await cvv.PressSequentiallyAsync("123", new() { Delay = 30 });
                                await Task.Delay(400);
                            }
                            else
                            {
                                var cardInput = targetFrame.Locator("input[id*='card' i], input[name*='card' i]").First;
                                if (await cardInput.CountAsync() > 0)
                                {
                                    await cardInput.ClickAsync();
                                    await cardInput.PressSequentiallyAsync("4111111111111111", new() { Delay = 30 });
                                }
                                var expInput = targetFrame.Locator("input[id*='exp' i], input[name*='exp' i]").First;
                                if (await expInput.CountAsync() > 0)
                                {
                                    await expInput.ClickAsync();
                                    await expInput.PressSequentiallyAsync("1228", new() { Delay = 30 });
                                }
                                var cvvInput = targetFrame.Locator("input[id*='cvv' i], input[name*='cvv' i], input[placeholder*='CVV' i]").First;
                                if (await cvvInput.CountAsync() > 0)
                                {
                                    await cvvInput.ClickAsync();
                                    await cvvInput.PressSequentiallyAsync("123", new() { Delay = 30 });
                                }
                            }

                            var linkBtn = targetFrame.Locator("button:has-text('Link Card'), button:has-text('Link'), button:has-text('Save')").Last;
                            if (await linkBtn.CountAsync() > 0 && await linkBtn.IsVisibleAsync())
                            {
                                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking 'Link Card' button");
                                lastActionTime = DateTime.UtcNow;
                                await linkBtn.ClickAsync(new() { Force = true });
                                await Task.Delay(4000);
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
                        var continueBtn = targetFrame.Locator("button:has-text('Continue')").Last;
                        if (await continueBtn.CountAsync() > 0 && await continueBtn.IsVisibleAsync())
                        {
                            lastActionTime = DateTime.UtcNow;
                            await continueBtn.ClickAsync(new() { Force = true });
                            await Task.Delay(3500);
                            continue;
                        }
                    }
                    // Case 4: Initial review or general step
                    else
                    {
                        var generalContinueBtn = targetFrame.Locator("button:has-text('Continue'):not(:has-text('Agree'))").Last;
                        var payBtn = targetFrame.Locator("button:has-text('Pay'), button:has-text('Pay Now'), button:has-text('Complete Purchase')").Last;

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
                            await Task.Delay(1500);
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
}
