using Microsoft.Playwright;
using Nop.Tests.E2E.Fixtures;
using NUnit.Framework;

namespace Nop.Tests.E2E.Tests;

[TestFixture]
[Category("CheckoutE2E")]
[Category("PayPal")]
[Category("Fastlane")]
public class FastlaneCheckoutTests : CheckoutTestBase
{
    private async Task AddItemToCartAsync(IPage page)
    {
        var productUrl = $"{Settings.BaseUrl.TrimEnd('/')}/{Settings.TestProductSlug.TrimStart('/')}";
        await page.GotoAsync(productUrl, new() { WaitUntil = WaitUntilState.Load });
        await page.EvaluateAsync("document.getElementById('eu-cookie-bar-notification')?.remove();");

        var addToCartButton = page.Locator(".add-to-cart-button, button[id^='add-to-cart-button']").First;
        await addToCartButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await addToCartButton.ClickAsync();

        // Allow AJAX cart update notification to process
        await Task.Delay(2500);
    }

    private async Task EnsureTermsAcceptedAsync(IPage page)
    {
        var termsCheckbox = page.Locator("#termsofservice");
        if (await termsCheckbox.CountAsync() > 0)
        {
            if (!await termsCheckbox.IsCheckedAsync())
            {
                await termsCheckbox.CheckAsync();
                await page.EvaluateAsync("const cb = document.getElementById('termsofservice'); if (cb && !cb.checked) { cb.checked = true; cb.dispatchEvent(new Event('change', { bubbles: true })); }");
            }
        }
    }

    [Test]
    [Description("Verifies that PayPal Fastlane inline elements (card, input, watermark) render on the cart page")]
    public async Task Fastlane_Elements_Render_On_Cart_Page()
    {
        await AddItemToCartAsync(Page);

        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        await Page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

        var fastlaneSection = Page.Locator("#fastlane-cart-section");
        await fastlaneSection.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        Assert.That(await fastlaneSection.IsVisibleAsync(), Is.True, "Fastlane cart section should be visible.");

        var emailInput = Page.Locator("#fastlane-email");
        Assert.That(await emailInput.IsVisibleAsync(), Is.True, "Fastlane email input should be visible.");

        var watermark = Page.Locator("#fastlane-watermark");
        Assert.That(await watermark.CountAsync(), Is.GreaterThan(0), "Fastlane watermark container should be present.");

        // Fastlane modal should remain hidden initially
        var modal = Page.Locator("#fastlane-intercept-modal");
        Assert.That(await modal.IsVisibleAsync(), Is.False, "Fastlane modal should not be visible initially.");
    }

    [Test]
    [Description("Verifies that clicking Proceed to Checkout without accepting terms displays terms validation rather than the Fastlane modal")]
    public async Task Checkout_Click_Without_Terms_Triggers_Terms_Validation_Not_Fastlane_Modal()
    {
        await AddItemToCartAsync(Page);

        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        await Page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

        var termsCheckbox = Page.Locator("#termsofservice");
        if (await termsCheckbox.CountAsync() > 0 && await termsCheckbox.IsCheckedAsync())
        {
            await termsCheckbox.UncheckAsync();
        }

        var checkoutButton = Page.Locator("#checkout");
        await checkoutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await checkoutButton.ClickAsync();

        // User stays on cart
        Assert.That(Page.Url, Does.Contain("/cart"), "User should remain on the cart page when terms are not accepted.");

        // Fastlane modal should NOT be displayed because terms failed first
        var modal = Page.Locator("#fastlane-intercept-modal");
        Assert.That(await modal.IsVisibleAsync(), Is.False, "Fastlane modal should not appear if terms of service are not accepted.");
    }

    [Test]
    [Description("Verifies that clicking Proceed to Checkout with terms accepted but empty email intercepts the user and opens the Fastlane modal")]
    public async Task Checkout_Click_With_Terms_And_Blank_Email_Opens_Fastlane_Modal()
    {
        await AddItemToCartAsync(Page);

        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        await Page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

        await EnsureTermsAcceptedAsync(Page);

        // Ensure email input is empty
        var emailInput = Page.Locator("#fastlane-email");
        await emailInput.FillAsync(string.Empty);

        var checkoutButton = Page.Locator("#checkout");
        await checkoutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await checkoutButton.ClickAsync();

        // Modal should now appear
        var modal = Page.Locator("#fastlane-intercept-modal");
        await modal.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.That(await modal.IsVisibleAsync(), Is.True, "Fastlane intercept modal should be displayed when clicking checkout with empty email.");

        var modalEmail = Page.Locator("#fastlane-modal-email");
        Assert.That(await modalEmail.IsVisibleAsync(), Is.True, "Modal email input should be visible.");

        var modalSubmitBtn = Page.Locator("#btn-fastlane-modal-submit");
        Assert.That(await modalSubmitBtn.IsVisibleAsync(), Is.True, "Modal submit button should be visible.");
    }

    [Test]
    [Description("Verifies that the Fastlane intercept modal can be closed/dismissed")]
    public async Task Fastlane_Modal_Can_Be_Dismissed()
    {
        await AddItemToCartAsync(Page);

        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        await Page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

        await EnsureTermsAcceptedAsync(Page);

        var checkoutButton = Page.Locator("#checkout");
        await checkoutButton.ClickAsync();

        var modal = Page.Locator("#fastlane-intercept-modal");
        await modal.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var closeBtn = Page.Locator("#btn-fastlane-close");
        await closeBtn.ClickAsync();

        await Task.Delay(300);
        Assert.That(await modal.IsVisibleAsync(), Is.False, "Fastlane modal should close after clicking close button.");
    }

    [Test]
    [Description("Verifies that entering a valid email inline on the cart proceeds smoothly into the checkout sequence")]
    public async Task Fastlane_Inline_Email_Proceeds_To_Checkout_Sequence()
    {
        await AddItemToCartAsync(Page);

        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        await Page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

        await EnsureTermsAcceptedAsync(Page);

        var emailInput = Page.Locator("#fastlane-email");
        await emailInput.FillAsync("fastlane.shopper.test@example.com");

        var checkoutButton = Page.Locator("#checkout");
        await checkoutButton.ClickAsync();

        // Wait for transition into checkout
        await Page.WaitForURLAsync(url => url.Contains("/checkout") || url.Contains("/onepagecheckout") || url.Contains("/login"), new() { Timeout = 15000 });

        Assert.Multiple(() =>
        {
            Assert.That(Page.Url, Does.Not.Contain("/cart"), "User should have transitioned past the cart page.");
            Assert.That(Page.Url, Does.Contain("/checkout").Or.Contain("/onepagecheckout").Or.Contain("checkoutAsGuest"), "Expected standard checkout or guest checkout destination.");
        });
    }

    [Test]
    [Description("Verifies that submitting email through the Fastlane intercept modal proceeds into the checkout sequence")]
    public async Task Fastlane_Modal_Email_Submission_Proceeds_To_Checkout_Sequence()
    {
        await AddItemToCartAsync(Page);

        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        await Page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

        await EnsureTermsAcceptedAsync(Page);

        var checkoutButton = Page.Locator("#checkout");
        await checkoutButton.ClickAsync();

        var modal = Page.Locator("#fastlane-intercept-modal");
        await modal.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var modalEmail = Page.Locator("#fastlane-modal-email");
        await modalEmail.FillAsync("fastlane.modal.shopper@example.com");

        var modalSubmitBtn = Page.Locator("#btn-fastlane-modal-submit");
        await modalSubmitBtn.ClickAsync();

        // Wait for transition into checkout
        await Page.WaitForURLAsync(url => url.Contains("/checkout") || url.Contains("/onepagecheckout") || url.Contains("/login"), new() { Timeout = 15000 });

        Assert.Multiple(() =>
        {
            Assert.That(Page.Url, Does.Not.Contain("/cart"), "User should have navigated past the cart.");
            Assert.That(Page.Url, Does.Contain("/checkout").Or.Contain("/onepagecheckout").Or.Contain("checkoutAsGuest"), "Expected standard checkout or guest checkout destination.");
        });
    }

    [Test]
    [Description("Verifies Fastlane inline capture and modal behavior on mobile viewports")]
    public async Task Fastlane_Mobile_Viewport_Interception_And_Modal()
    {
        var (mobileContext, mobilePage) = await CreateMobileContextAsync(390, 844);
        try
        {
            await AddItemToCartAsync(mobilePage);

            var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
            await mobilePage.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

            await EnsureTermsAcceptedAsync(mobilePage);

            var emailInput = mobilePage.Locator("#fastlane-email");
            await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            Assert.That(await emailInput.IsVisibleAsync(), Is.True, "Fastlane email input should be visible on mobile.");

            // Click checkout to open intercept modal
            var checkoutButton = mobilePage.Locator("#checkout");
            await checkoutButton.ClickAsync();

            var modal = mobilePage.Locator("#fastlane-intercept-modal");
            await modal.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            Assert.That(await modal.IsVisibleAsync(), Is.True, "Fastlane modal should open on mobile.");

            // Enter email in modal and continue
            var modalEmail = mobilePage.Locator("#fastlane-modal-email");
            await modalEmail.FillAsync("fastlane.mobile.user@example.com");

            var modalSubmitBtn = mobilePage.Locator("#btn-fastlane-modal-submit");
            await modalSubmitBtn.ClickAsync();

            await mobilePage.WaitForURLAsync(url => url.Contains("/checkout") || url.Contains("/onepagecheckout") || url.Contains("/login"), new() { Timeout = 15000 });
            Assert.That(mobilePage.Url, Does.Not.Contain("/cart"), "Mobile user should have transitioned past cart.");
        }
        finally
        {
            await mobileContext.CloseAsync();
        }
    }
}
