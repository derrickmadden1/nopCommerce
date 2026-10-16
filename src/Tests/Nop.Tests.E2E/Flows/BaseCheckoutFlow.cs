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

    protected async Task<string> WaitForOrderCompletionAndGetNumberAsync(IPage page, int timeoutSeconds = 30)
    {
        // Wait for completed URL
        await page.WaitForURLAsync(
            url => url.ToString().Contains("/checkout/completed", StringComparison.OrdinalIgnoreCase),
            new() { Timeout = timeoutSeconds * 1000, WaitUntil = WaitUntilState.DOMContentLoaded });

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
