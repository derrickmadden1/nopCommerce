using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Nop.Tests.E2E.Configuration;
using NUnit.Framework;

namespace Nop.Tests.E2E.Flows;

public abstract class BaseCheckoutFlow : ICheckoutFlow
{
    protected readonly CheckoutTestSettings Settings;

    protected BaseCheckoutFlow(CheckoutTestSettings? settings = null)
    {
        Settings = settings ?? TestConfig.Current;
    }

    public abstract string RouteName { get; }

    public abstract Task<CheckoutResult> RunAsync(IPage page, CancellationToken cancellationToken = default);

    protected async Task AddTestProductToCartAsync(IPage page)
    {
        var productUrl = $"{Settings.BaseUrl.TrimEnd('/')}/{Settings.TestProductSlug.TrimStart('/')}";

        await page.GotoAsync(productUrl, new() { WaitUntil = WaitUntilState.Load });
        await DismissCookieNotificationAsync(page);

        // Click Add to cart
        var addToCartButton = page.Locator(".add-to-cart-button, button[id^='add-to-cart-button']").First;
        await addToCartButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await addToCartButton.ClickAsync(new() { Force = true });

        // Wait for AJAX cart notification / update
        await Task.Delay(2500);
    }

    protected async Task ProceedToCartAndAcceptTermsAsync(IPage page)
    {
        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        await page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });
        await DismissCookieNotificationAsync(page);

        // Accept terms checkbox (handles themes where input checkbox is styled or hidden)
        var termsCheckbox = page.Locator("#termsofservice");
        if (await termsCheckbox.CountAsync() > 0)
        {
            if (!await termsCheckbox.IsCheckedAsync())
            {
                var termsLabel = page.Locator("label[for='termsofservice']");
                if (await termsLabel.CountAsync() > 0 && await termsLabel.IsVisibleAsync())
                {
                    await termsLabel.ClickAsync(new() { Force = true });
                }
                else
                {
                    await termsCheckbox.CheckAsync(new() { Force = true });
                }

                await page.EvaluateAsync("const cb = document.getElementById('termsofservice'); if (cb && !cb.checked) { cb.checked = true; cb.dispatchEvent(new Event('change', { bubbles: true })); }");
            }
        }

        // Proceed to Checkout
        var checkoutButton = page.Locator("#checkout, button[name='checkout'], button:has-text('Checkout'), button:has-text('PROCEED TO CHECKOUT')").First;
        await checkoutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await checkoutButton.ClickAsync(new() { Force = true });

        // Wait for transition away from cart
        await page.WaitForURLAsync(url => !url.ToString().EndsWith("/cart", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.Load);
    }

    protected async Task HandleGuestCheckoutIfPromptedAsync(IPage page)
    {
        await DismissCookieNotificationAsync(page);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] HandleGuestCheckoutIfPromptedAsync entering, url={page.Url}");

        if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
            page.Url.Contains("checkoutasguest", StringComparison.OrdinalIgnoreCase))
        {
            var guestButton = page.Locator(".checkout-as-guest-button, input.checkout-as-guest-button, button:has-text('Checkout as Guest')").First;
            if (await guestButton.IsVisibleAsync())
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking Guest Checkout button");
                await guestButton.ClickAsync();
                await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            }
        }

        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] HandleGuestCheckoutIfPromptedAsync finished, url={page.Url}");
    }

    protected async Task HandleReturningCustomerLoginIfPromptedAsync(IPage page, string? email = null, string? password = null)
    {
        await DismissCookieNotificationAsync(page);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] HandleReturningCustomerLoginIfPromptedAsync entering, url={page.Url}");

        if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
            page.Url.Contains("checkoutasguest", StringComparison.OrdinalIgnoreCase))
        {
            var customer = Settings.Customer;
            var loginEmail = email ?? customer.ReturningCustomerEmail;
            var loginPassword = password ?? customer.ReturningCustomerPassword;

            var emailInput = page.Locator("#Email, input[name='Email'], input.email").First;
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await emailInput.FillAsync(loginEmail);

            var passwordInput = page.Locator("#Password, input[name='Password'], input.password").First;
            await passwordInput.FillAsync(loginPassword);

            var loginButton = page.Locator("button.login-btn-modern, button[type='submit']:has-text('Log in'), input[value='Log in'], .returning-customer-block button[type='submit']").First;
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Submitting returning customer login for {loginEmail}");
            await loginButton.ClickAsync();

            await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            // Ensure returning customer phone is E.164 compliant (+44...) for Square API customer creation
            await EnsureCustomerProfilePhoneValidAsync(page);

            // If not already in checkout, proceed to cart and checkout
            if (!page.Url.Contains("/checkout", StringComparison.OrdinalIgnoreCase))
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Proceeding to cart after login and profile check");
                await ProceedToCartAndAcceptTermsAsync(page);
            }
        }

        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] HandleReturningCustomerLoginIfPromptedAsync finished, url={page.Url}");
    }

    public async Task EnsureCustomerProfilePhoneValidAsync(IPage page)
    {
        try
        {
            var infoUrl = $"{Settings.BaseUrl.TrimEnd('/')}/customer/info";
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigating to customer info: {infoUrl}");
            await page.GotoAsync(infoUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            // Check if we were redirected to login
            if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
            {
                await HandleReturningCustomerLoginIfPromptedAsync(page);
                await page.GotoAsync(infoUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            }

            var phoneInput = page.Locator("#Phone");
            if (await phoneInput.IsVisibleAsync())
            {
                var curVal = await phoneInput.InputValueAsync();
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Current customer profile phone is '{curVal}'");
                if (string.IsNullOrEmpty(curVal) || !curVal.StartsWith("+"))
                {
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Updating customer profile phone to '+447700900123'");
                    await phoneInput.FillAsync("+447700900123");
                    var saveBtn = page.Locator("#save-info-button, button.save-customer-info-button").First;
                    await saveBtn.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Customer profile saved successfully");
                }
            }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] EnsureCustomerProfilePhoneValidAsync non-fatal: {ex.Message}");
        }
    }

    protected async Task HandleNewCustomerRegistrationIfPromptedAsync(IPage page, string? firstName = null, string? lastName = null, string? email = null, string? password = null)
    {
        await DismissCookieNotificationAsync(page);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] HandleNewCustomerRegistrationIfPromptedAsync entering, url={page.Url}");

        if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
            page.Url.Contains("checkoutasguest", StringComparison.OrdinalIgnoreCase))
        {
            var registerNavBtn = page.Locator(".new-customer-block button:has-text('Register'), .new-customer-content button:has-text('Register'), a[href*='/register'], button[onclick*='register']").First;
            await registerNavBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking Register button from checkout prompt");
            await registerNavBtn.ClickAsync();

            await page.WaitForURLAsync(url => url.Contains("/register", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await DismissCookieNotificationAsync(page);

            var customer = Settings.Customer;
            var regEmail = email ?? $"e2e-reg-{DateTime.UtcNow:yyyyMMddHHmmssfff}@rosecottagecroft.co.uk";
            var regPassword = password ?? "Password123!";

            var firstNameInput = page.Locator("#FirstName");
            if (await firstNameInput.IsVisibleAsync())
                await firstNameInput.FillAsync(firstName ?? customer.FirstName);

            var lastNameInput = page.Locator("#LastName");
            if (await lastNameInput.IsVisibleAsync())
                await lastNameInput.FillAsync(lastName ?? customer.LastName);

            var emailInput = page.Locator("#Email");
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await emailInput.FillAsync(regEmail);

            var passwordInput = page.Locator("#Password");
            await passwordInput.FillAsync(regPassword);

            var confirmPasswordInput = page.Locator("#ConfirmPassword");
            if (await confirmPasswordInput.IsVisibleAsync())
                await confirmPasswordInput.FillAsync(regPassword);

            var consentInput = page.Locator("#accept-consent");
            if (await consentInput.IsVisibleAsync())
                await consentInput.CheckAsync(new() { Force = true });

            var registerSubmitBtn = page.Locator("#register-button, button.register-btn-modern, button[name='register-button']").First;
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Submitting registration for new customer: {regEmail}");
            await registerSubmitBtn.ClickAsync();

            await page.WaitForURLAsync(url => !url.EndsWith("/register", StringComparison.OrdinalIgnoreCase), new() { Timeout = 20000 });
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            if (page.Url.Contains("registerresult", StringComparison.OrdinalIgnoreCase))
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] On registerresult page, clicking Continue button");
                var continueBtn = page.Locator(".register-continue-button, a:has-text('Continue'), button:has-text('Continue')").First;
                if (await continueBtn.IsVisibleAsync())
                {
                    await continueBtn.ClickAsync();
                    await page.WaitForURLAsync(url => !url.Contains("registerresult", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                }
            }

            if (page.Url.Contains("/cart", StringComparison.OrdinalIgnoreCase))
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Back on cart after registration, clicking checkout as authenticated user");
                await ProceedToCartAndAcceptTermsAsync(page);
            }
        }

        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] HandleNewCustomerRegistrationIfPromptedAsync finished, url={page.Url}");
    }

    protected async Task HandleAuthenticationStepAsync(IPage page, CheckoutAuthMode authMode)
    {
        switch (authMode)
        {
            case CheckoutAuthMode.Guest:
                await HandleGuestCheckoutIfPromptedAsync(page);
                break;
            case CheckoutAuthMode.ReturningCustomer:
                await HandleReturningCustomerLoginIfPromptedAsync(page);
                break;
            case CheckoutAuthMode.NewCustomerRegistration:
                await HandleNewCustomerRegistrationIfPromptedAsync(page);
                break;
        }
    }

    public async Task NavigateToPaymentInfoAsync(IPage page, CheckoutAuthMode authMode, string paymentMethodSystemName = "Payments.Square")
    {
        await AddTestProductToCartAsync(page);
        await ProceedToCartAndAcceptTermsAsync(page);
        await HandleAuthenticationStepAsync(page, authMode);
        await FillBillingAddressIfRequiredAsync(page);
        await FillShippingAddressIfRequiredAsync(page);
        await SelectShippingMethodAsync(page);
        await SelectPaymentMethodAsync(page, paymentMethodSystemName);
    }

    protected async Task FillBillingAddressIfRequiredAsync(IPage page)
    {
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] FillBillingAddressIfRequiredAsync entering, url={page.Url}");
        if (!page.Url.Contains("/checkout/billingaddress", StringComparison.OrdinalIgnoreCase))
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Not on billingaddress page, skipping");
            return;
        }

        await DismissCookieNotificationAsync(page);

        // Check if there is an existing address button to select
        var existingBtn = page.Locator("#billing-addresses-modern .select-address-btn, .billing-addresses .select-address-btn, .select-billing-address-button").First;
        if (await existingBtn.CountAsync() > 0)
        {
            try
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Existing billing address found, clicking select button");
                await existingBtn.ClickAsync(new() { Force = true });
                await page.WaitForURLAsync(url => !url.Contains("/checkout/billingaddress", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Selected existing billing address, new url={page.Url}");
                return;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Existing address click fallback: {ex.Message}");
            }
        }

        var firstNameField = page.Locator("#BillingNewAddress_FirstName");
        await firstNameField.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var customer = Settings.Customer;
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Filling new billing address for {customer.FirstName} {customer.LastName}");
        await firstNameField.FillAsync(customer.FirstName);
        await page.Locator("#BillingNewAddress_LastName").FillAsync(customer.LastName);
        await page.Locator("#BillingNewAddress_Email").FillAsync(customer.Email);

        var company = page.Locator("#BillingNewAddress_Company");
        if (await company.IsVisibleAsync())
            await company.FillAsync(customer.Company);

        var countryDropdown = page.Locator("#BillingNewAddress_CountryId");
        if (await countryDropdown.IsVisibleAsync())
        {
            await countryDropdown.SelectOptionAsync(new[] { new SelectOptionValue { Label = customer.Country } });
            await Task.Delay(1500); // Wait for state/county cascade
        }

        var stateDropdown = page.Locator("#BillingNewAddress_StateProvinceId");
        if (await stateDropdown.IsVisibleAsync() && await stateDropdown.Locator("option").CountAsync() > 1)
        {
            await stateDropdown.SelectOptionAsync(new[] { new SelectOptionValue { Index = 1 } });
        }

        var countyField = page.Locator("#BillingNewAddress_County");
        if (await countyField.IsVisibleAsync())
            await countyField.FillAsync(customer.County);

        await page.Locator("#BillingNewAddress_City").FillAsync(customer.City);
        await page.Locator("#BillingNewAddress_Address1").FillAsync(customer.Address1);
        await page.Locator("#BillingNewAddress_ZipPostalCode").FillAsync(customer.ZipPostalCode);
        await page.Locator("#BillingNewAddress_PhoneNumber").FillAsync(customer.PhoneNumber);

        // Save Billing Address
        var billingNext = page.Locator("#billingaddress-next-button, .billing-address-page button[name='nextstep'], #billing-buttons-container .new-address-next-step-button").Locator(":visible").First;
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for billing Next button to be visible");
        await billingNext.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking billing Next button");
        await billingNext.ClickAsync(new() { Force = true });

        await page.WaitForURLAsync(url => !url.Contains("/checkout/billingaddress", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Left billingaddress page, new url={page.Url}");
    }

    protected async Task FillShippingAddressIfRequiredAsync(IPage page)
    {
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] FillShippingAddressIfRequiredAsync entering, url={page.Url}");
        if (!page.Url.Contains("/checkout/shippingaddress", StringComparison.OrdinalIgnoreCase))
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Not on shippingaddress page, skipping");
            return;
        }

        await DismissCookieNotificationAsync(page);

        // Check if there is an existing address button to select
        var existingBtn = page.Locator("#shipping-addresses-form .select-address-btn, .shipping-addresses .select-address-btn, .select-shipping-address-button").First;
        if (await existingBtn.CountAsync() > 0)
        {
            try
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Existing shipping address found, clicking select button");
                await existingBtn.ClickAsync(new() { Force = true });
                await page.WaitForURLAsync(url => !url.Contains("/checkout/shippingaddress", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Selected existing shipping address, new url={page.Url}");
                return;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Existing shipping address click fallback: {ex.Message}");
            }
        }

        var shippingFirstName = page.Locator("#ShippingNewAddress_FirstName");
        if (await shippingFirstName.IsVisibleAsync())
        {
            var customer = Settings.Customer;
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Filling new shipping address for {customer.FirstName} {customer.LastName}");
            await shippingFirstName.FillAsync(customer.FirstName);
            await page.Locator("#ShippingNewAddress_LastName").FillAsync(customer.LastName);
            await page.Locator("#ShippingNewAddress_Email").FillAsync(customer.Email);

            var countryDropdown = page.Locator("#ShippingNewAddress_CountryId");
            if (await countryDropdown.IsVisibleAsync())
            {
                await countryDropdown.SelectOptionAsync(new[] { new SelectOptionValue { Label = customer.Country } });
                await Task.Delay(1500);
            }

            var stateDropdown = page.Locator("#ShippingNewAddress_StateProvinceId");
            if (await stateDropdown.IsVisibleAsync() && await stateDropdown.Locator("option").CountAsync() > 1)
            {
                await stateDropdown.SelectOptionAsync(new[] { new SelectOptionValue { Index = 1 } });
            }

            var countyField = page.Locator("#ShippingNewAddress_County");
            if (await countyField.IsVisibleAsync())
                await countyField.FillAsync(customer.County);

            await page.Locator("#ShippingNewAddress_City").FillAsync(customer.City);
            await page.Locator("#ShippingNewAddress_Address1").FillAsync(customer.Address1);
            await page.Locator("#ShippingNewAddress_ZipPostalCode").FillAsync(customer.ZipPostalCode);
            await page.Locator("#ShippingNewAddress_PhoneNumber").FillAsync(customer.PhoneNumber);
        }

        var shippingNext = page.Locator("#shippingaddress-next-button, .shipping-address-page button[name='nextstep'], #shipping-buttons-container .new-address-next-step-button").Locator(":visible").First;
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for shipping Next button to be visible");
        await shippingNext.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking shipping Next button");
        await shippingNext.ClickAsync(new() { Force = true });

        await page.WaitForURLAsync(url => !url.Contains("/checkout/shippingaddress", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Left shippingaddress page, new url={page.Url}");
    }

    protected async Task SelectShippingMethodAsync(IPage page)
    {
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] SelectShippingMethodAsync entering, url={page.Url}");
        if (!page.Url.Contains("/checkout/shippingmethod", StringComparison.OrdinalIgnoreCase))
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Not on shippingmethod page, skipping");
            return;
        }

        await DismissCookieNotificationAsync(page);

        // Select first available shipping option if not selected
        var shippingOptions = page.Locator(".shipping-method-page input[name='shippingoption'], input[name='shippingoption']");
        if (await shippingOptions.CountAsync() > 0)
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Checking first shipping option");
            await shippingOptions.First.CheckAsync(new() { Force = true });
        }

        var shippingMethodNext = page.Locator(".shipping-method-page button[name='nextstep'], #shipping-method-buttons-container .shipping-method-next-step-button, button[onclick*='ShippingMethod.save']").First;
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for shipping method Next button");
        await shippingMethodNext.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking shipping method Next button");
        await shippingMethodNext.ClickAsync(new() { Force = true });

        await page.WaitForURLAsync(url => !url.Contains("/checkout/shippingmethod", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Left shippingmethod page, new url={page.Url}");
    }

    protected async Task SelectPaymentMethodAsync(IPage page, string paymentMethodSystemName)
    {
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] SelectPaymentMethodAsync entering for {paymentMethodSystemName}, url={page.Url}");
        if (!page.Url.Contains("/checkout/paymentmethod", StringComparison.OrdinalIgnoreCase))
            return;

        await DismissCookieNotificationAsync(page);

        var radio = page.Locator($"input[value='{paymentMethodSystemName}']").First;
        await radio.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 15000 });

        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Selecting {paymentMethodSystemName} payment method");
        if (!await radio.IsCheckedAsync())
        {
            await radio.CheckAsync(new() { Force = true });
        }

        var nextBtn = page.Locator(".payment-method-page button[name='nextstep'], #payment-method-buttons-container .payment-method-next-step-button, button[onclick*='PaymentMethod.save']").Locator(":visible").First;
        await nextBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking payment method Next button");
        await nextBtn.ClickAsync(new() { Force = true });

        await page.WaitForURLAsync(url => !url.Contains("/checkout/paymentmethod", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Left paymentmethod page, new url={page.Url}");
    }

    protected async Task FillSquarePaymentDetailsAsync(
        IPage page,
        bool saveCard = false,
        bool useStoredCard = false,
        string? customCardNumber = null)
    {
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] FillSquarePaymentDetailsAsync entering, url={page.Url}");
        if (!page.Url.Contains("/checkout/paymentinfo", StringComparison.OrdinalIgnoreCase))
            return;

        await DismissCookieNotificationAsync(page);

        // Stored card flow (PR #269 card-on-file)
        if (useStoredCard)
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Using stored card on file");
            var storedCardSelect = page.Locator("#StoredCardId, select[name='StoredCardId']");
            try
            {
                await storedCardSelect.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            }
            catch (Exception ex) when (ex.Message.Contains("Timeout") || ex is TimeoutException)
            {
                throw new InvalidOperationException("UseStoredCard was requested, but no saved card dropdown (#StoredCardId) was found on the payment info page.");
            }

            // Ensure options exist
            var options = await storedCardSelect.Locator("option").AllAsync();
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Found {options.Count} stored card options");

            // Look for non-empty option
            string? cardVal = null;
            foreach (var opt in options)
            {
                var val = await opt.GetAttributeAsync("value");
                if (!string.IsNullOrEmpty(val) && !val.Equals(Guid.Empty.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    cardVal = val;
                    break;
                }
            }

            if (string.IsNullOrEmpty(cardVal))
            {
                throw new InvalidOperationException("UseStoredCard was requested, but no saved card token was found in #StoredCardId dropdown.");
            }

            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Selecting stored card ID: {cardVal}");
            await storedCardSelect.SelectOptionAsync(new[] { cardVal });

            // Verify #payment-form is hidden as expected per toggleStoredCard()
            var paymentForm = page.Locator("#payment-form");
            var isPaymentFormHidden = await paymentForm.IsHiddenAsync();
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] #payment-form is hidden: {isPaymentFormHidden}");

            // Wait for Square SDK initialization to complete
            try
            {
                await page.Locator(".ajax-loading-block-window").WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });
            }
            catch { }
            await Task.Delay(1000);

            var payButton = page.Locator("#pay-button").First;
            await payButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking #pay-button for stored card");
            await payButton.ClickAsync();

            if (Settings.Square.Handle3ds)
            {
                TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Checking for 3DS CardinalCommerce modal (stored card)");
                await HandleCardinal3dsModalIfPresentAsync(page, Settings.Square.ThreeDsSmsCode);
            }

            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for transition away from paymentinfo");
            await page.WaitForURLAsync(url => !url.Contains("/checkout/paymentinfo", StringComparison.OrdinalIgnoreCase), new() { Timeout = 25000 });
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Transitioned to confirm, new url={page.Url}");
            return;
        }

        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for Square #card-container iframe");
        var cardIframe = page.FrameLocator("#card-container iframe");

        var cardNumberInput = cardIframe.Locator("input[name='cardNumber'], input[autocomplete='cc-number'], input[placeholder*='Card number']").First;
        await cardNumberInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 25000 });

        var square = Settings.Square;
        var cardNumberToUse = customCardNumber ?? square.TestCardNumber;
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Entering Square test card: {cardNumberToUse[..4]}****");

        await cardNumberInput.ClickAsync();
        await cardNumberInput.FillAsync(cardNumberToUse);

        var expInput = cardIframe.Locator("input[name='expirationDate'], input[autocomplete='cc-exp'], input[placeholder*='MM/YY']").First;
        if (await expInput.IsVisibleAsync())
        {
            await expInput.ClickAsync();
            await expInput.FillAsync(square.Expiration);
        }

        var cvvInput = cardIframe.Locator("input[name='cvv'], input[autocomplete='cc-csc'], input[placeholder*='CVV']").First;
        if (await cvvInput.IsVisibleAsync())
        {
            await cvvInput.ClickAsync();
            await cvvInput.FillAsync(square.Cvv);
        }

        var postalInput = cardIframe.Locator("input[name='postalCode'], input[autocomplete='postal-code'], input[placeholder*='Postal']").First;
        if (await postalInput.IsVisibleAsync())
        {
            await postalInput.ClickAsync();
            await postalInput.FillAsync(square.PostalCode);
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Postal code field filled with {square.PostalCode}");
        }

        if (saveCard)
        {
            var saveCardCheckbox = page.Locator("#SaveCard, input[name='SaveCard']").First;
            await saveCardCheckbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Checking 'Save card' checkbox");
            if (!await saveCardCheckbox.IsCheckedAsync())
            {
                await saveCardCheckbox.CheckAsync();
            }
        }

        var payBtn = page.Locator("#pay-button").First;
        await payBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking #pay-button for Square tokenization");
        await payBtn.ClickAsync();

        if (square.Handle3ds)
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Checking for 3DS CardinalCommerce modal");
            await HandleCardinal3dsModalIfPresentAsync(page, square.ThreeDsSmsCode);
        }

        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for transition away from paymentinfo");
        await page.WaitForURLAsync(url => !url.Contains("/checkout/paymentinfo", StringComparison.OrdinalIgnoreCase), new() { Timeout = 25000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Transitioned to confirm, new url={page.Url}");
    }

    protected async Task HandleCardinal3dsModalIfPresentAsync(IPage page, string smsCode)
    {
        try
        {
            var cardinalFrame = page.FrameLocator("#Cardinal-CCA-IFrame");
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

    protected async Task ConfirmOrderAsync(IPage page)
    {
        if (!page.Url.Contains("/checkout/confirm", StringComparison.OrdinalIgnoreCase))
            return;

        await DismissCookieNotificationAsync(page);

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

        var confirmButton = page.Locator(".confirm-order-btn-modern, .order-confirm-page button[name='nextstep'], #confirm-order-buttons-container .confirm-order-next-step-button, button[onclick*='ConfirmOrder.save'], button:has-text('Confirm')").Locator(":visible").First;
        await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking confirm order button");
        await confirmButton.ClickAsync(new() { Force = true });
    }

    protected async Task<string> WaitForOrderCompletionAndGetNumberAsync(IPage page, int timeoutSeconds = 30)
    {
        try
        {
            // Wait for completed URL
            await page.WaitForURLAsync(
                url => url.ToString().Contains("/checkout/completed", StringComparison.OrdinalIgnoreCase),
                new() { Timeout = timeoutSeconds * 1000, WaitUntil = WaitUntilState.DOMContentLoaded });
        }
        catch (TimeoutException)
        {
            var errorElement = page.Locator(".message-error, .order-confirm-page .message-error li, .message-error li").First;
            if (await errorElement.IsVisibleAsync())
            {
                var text = (await errorElement.InnerTextAsync()).Trim();
                throw new InvalidOperationException($"Checkout failed on confirm step with error: '{text}'");
            }
            throw;
        }

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Extract order number from page
        var pageText = await page.InnerTextAsync("body");
        var match = Regex.Match(pageText, @"Order number:\s*#?([0-9A-Za-z-]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        var orderDetailsLink = page.Locator("a[href*='/orderdetails/']").First;
        if (await orderDetailsLink.IsVisibleAsync())
        {
            var href = await orderDetailsLink.GetAttributeAsync("href") ?? "";
            var linkMatch = Regex.Match(href, @"/orderdetails/([0-9]+)");
            if (linkMatch.Success)
                return linkMatch.Groups[1].Value;
        }

        return "COMPLETED";
    }

    public async Task<string> WaitForPaymentFailureMessageAsync(IPage page, int timeoutSeconds = 25)
    {
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Waiting for payment failure message on confirm page");
        var errorLocator = page.Locator(".message-error, .order-confirm-page .message-error li, .message-error li").First;
        await errorLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutSeconds * 1000 });
        var text = (await errorLocator.InnerTextAsync()).Trim();
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Captured payment failure message: {text}");
        return text;
    }

    protected async Task DismissCookieNotificationAsync(IPage page)
    {
        try
        {
            await page.EvaluateAsync("document.getElementById('eu-cookie-bar-notification')?.remove();");
            var okBtn = page.Locator("#eu-cookie-ok");
            if (await okBtn.IsVisibleAsync())
            {
                await okBtn.ClickAsync();
                await Task.Delay(500);
            }
        }
        catch
        {
            // Ignore cookie dismissal failures
        }
    }

    public async Task ClearCartAsync(IPage page)
    {
        try
        {
            var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
            await page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Commit });
            await Task.Delay(1000);

            var removeBtns = page.Locator(".remove-btn, .remove-from-cart input[type='checkbox'], .remove-from-cart button");
            while (await removeBtns.CountAsync() > 0)
            {
                var first = removeBtns.First;
                if (await first.IsVisibleAsync())
                {
                    await first.ClickAsync();
                    await Task.Delay(1000);
                }
                else
                {
                    break;
                }
            }
        }
        catch
        {
            // Suppress cleanup exceptions
        }
    }
}
