using Microsoft.Playwright;
using Nop.Tests.E2E.Fixtures;
using NUnit.Framework;

namespace Nop.Tests.E2E.Tests;

[TestFixture]
[Category("CheckoutE2E")]
[Category("CartValidation")]
public class CartValidationAndRecoveryTests : CheckoutTestBase
{
    private static async Task ProceedPastCartAsync(IPage page, ILocator checkoutButton)
    {
        await checkoutButton.ClickAsync();

        // If PayPal Fastlane intercept modal appears when proceeding with empty email, click skip to proceed to standard checkout
        var fastlaneSkipBtn = page.Locator("#btn-fastlane-skip");
        try
        {
            await fastlaneSkipBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 1500 });
            await fastlaneSkipBtn.ClickAsync(new() { Force = true });
        }
        catch
        {
            // Fastlane modal not triggered or not enabled
        }

        await page.WaitForURLAsync(url => !url.EndsWith("/cart", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.Load);
    }

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

    [Test]
    [Description("Verifies that on mobile viewports, proceeding without terms triggers an inline error highlight and banner instead of an obscuring center-screen dialog")]
    public async Task Cart_Without_Terms_Shows_Inline_Validation_On_Mobile()
    {
        var (mobileContext, mobilePage) = await CreateMobileContextAsync(390, 844);
        try
        {
            await AddItemToCartAsync(mobilePage);

            var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
            await mobilePage.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

            var termsCheckbox = mobilePage.Locator("#termsofservice");
            await termsCheckbox.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 10000 });
            Assert.That(await termsCheckbox.IsCheckedAsync(), Is.False, "Terms checkbox should not be checked by default.");

            // Verify floating chat bubble is hidden on mobile cart to avoid intercepting clicks
            var chatBubble = mobilePage.Locator("#ai-chat-bubble");
            if (await chatBubble.CountAsync() > 0)
            {
                var isChatVisible = await chatBubble.IsVisibleAsync();
                Assert.That(isChatVisible, Is.False, "#ai-chat-bubble should be hidden on shopping cart page to prevent overlapping checkout buttons.");
            }

            // Click Proceed to Checkout without checking terms (natural click without force to test hit-testing)
            var checkoutButton = mobilePage.Locator("#checkout");
            await checkoutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await checkoutButton.ClickAsync();

            // Assert page remains on cart
            Assert.That(mobilePage.Url, Does.Contain("/cart"), "User should remain on cart page when terms are not accepted.");

            // Assert inline error banner is visible
            var errorBanner = mobilePage.Locator("#terms-of-service-error");
            await errorBanner.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            var errorText = await errorBanner.InnerTextAsync();
            Assert.That(errorText, Does.Contain("terms of service").IgnoreCase, "Expected inline terms validation error message.");

            // Assert terms container has error highlight class
            var termsContainer = mobilePage.Locator("#terms-of-service-container");
            var classAttr = await termsContainer.GetAttributeAsync("class") ?? "";
            Assert.That(classAttr, Does.Contain("terms-error-highlight"), "Terms container should have terms-error-highlight class.");

            // Assert jQuery UI center-screen dialog is NOT open over the checkbox
            var warningDialog = mobilePage.Locator(".ui-dialog:visible");
            Assert.That(await warningDialog.CountAsync(), Is.EqualTo(0), "jQuery UI dialog should not be opened over the terms checkbox.");
        }
        catch (Exception)
        {
            await CaptureMobileScreenshotAsync(mobilePage, nameof(Cart_Without_Terms_Shows_Inline_Validation_On_Mobile));
            throw;
        }
        finally
        {
            await mobileContext.CloseAsync();
        }
    }

    [Test]
    [Description("Verifies that a mobile user can tap the inline error banner to accept terms and proceed to checkout")]
    public async Task Cart_User_Can_Recover_By_Tapping_Inline_Error_Banner_On_Mobile()
    {
        var (mobileContext, mobilePage) = await CreateMobileContextAsync(390, 844);
        try
        {
            await AddItemToCartAsync(mobilePage);

            var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
            await mobilePage.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

            var checkoutButton = mobilePage.Locator("#checkout");
            await checkoutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await checkoutButton.ClickAsync();

            // Wait for inline error banner
            var errorBanner = mobilePage.Locator("#terms-of-service-error");
            await errorBanner.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            // User taps the inline error banner to recover
            TestContext.WriteLine("Tapping inline error banner to accept terms...");
            await errorBanner.ClickAsync();

            // Verify terms checkbox is now checked
            var termsCheckbox = mobilePage.Locator("#termsofservice");
            Assert.That(await termsCheckbox.IsCheckedAsync(), Is.True, "Clicking inline error banner should check #termsofservice.");

            // Now click Proceed to Checkout again
            await ProceedPastCartAsync(mobilePage, checkoutButton);
            Assert.That(mobilePage.Url, Does.Not.EndWith("/cart"), "User should successfully transition to checkout after accepting terms.");
        }
        catch (Exception)
        {
            await CaptureMobileScreenshotAsync(mobilePage, nameof(Cart_User_Can_Recover_By_Tapping_Inline_Error_Banner_On_Mobile));
            throw;
        }
        finally
        {
            await mobileContext.CloseAsync();
        }
    }

    [Test]
    [Description("Verifies that tapping the modern terms card container toggles the checkbox and dismisses error highlight")]
    public async Task Cart_User_Can_Recover_By_Tapping_Terms_Card_Container()
    {
        var (mobileContext, mobilePage) = await CreateMobileContextAsync(390, 844);
        try
        {
            await AddItemToCartAsync(mobilePage);

            var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
            await mobilePage.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

            var checkoutButton = mobilePage.Locator("#checkout");
            await checkoutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await checkoutButton.ClickAsync();

            var errorBanner = mobilePage.Locator("#terms-of-service-error");
            await errorBanner.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

            // Allow shake/layout animation to finish before tapping
            await mobilePage.WaitForTimeoutAsync(400);

            // Tap on the terms card text (avoiding the separate 'read' link)
            var termsText = mobilePage.Locator("#terms-of-service-container .terms-label-text");
            if (await termsText.CountAsync() > 0)
            {
                await termsText.ClickAsync();
            }
            else
            {
                var termsContainer = mobilePage.Locator("#terms-of-service-container");
                await termsContainer.ClickAsync(new() { Position = new() { X = 40, Y = 20 } });
            }

            var termsCheckbox = mobilePage.Locator("#termsofservice");
            var isChecked = false;
            for (var i = 0; i < 30 && !isChecked; i++)
            {
                isChecked = await termsCheckbox.IsCheckedAsync();
                if (!isChecked) await Task.Delay(100);
            }
            Assert.That(isChecked, Is.True, "Tapping terms container card should check #termsofservice.");

            await errorBanner.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 3000 });

            // Proceed successfully
            await ProceedPastCartAsync(mobilePage, checkoutButton);
            Assert.That(mobilePage.Url, Does.Not.EndWith("/cart"));
        }
        catch (Exception)
        {
            await CaptureMobileScreenshotAsync(mobilePage, nameof(Cart_User_Can_Recover_By_Tapping_Terms_Card_Container));
            throw;
        }
        finally
        {
            await mobileContext.CloseAsync();
        }
    }

    private static async Task CaptureMobileScreenshotAsync(IPage mobilePage, string testName)
    {
        try
        {
            var screenshotsDir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
            Directory.CreateDirectory(screenshotsDir);
            var filePath = Path.Combine(screenshotsDir, $"{testName}_Mobile_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
            await mobilePage.ScreenshotAsync(new() { Path = filePath, FullPage = true });
            TestContext.WriteLine($"[MOBILE SCREENSHOT CAPTURED] {filePath}");
            TestContext.AddTestAttachment(filePath, "Mobile Failure Screenshot");
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Failed to capture mobile screenshot: {ex.Message}");
        }
    }

    [Test]
    [Description("Verifies terms inline validation and error recovery on standard desktop viewports")]
    public async Task Cart_Desktop_Terms_Validation_And_Recovery()
    {
        await AddItemToCartAsync(Page);

        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        await Page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });

        var checkoutButton = Page.Locator("#checkout");
        await checkoutButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        // Click checkout without terms
        await checkoutButton.ClickAsync();

        // Verify error banner is visible
        var errorBanner = Page.Locator("#terms-of-service-error");
        await errorBanner.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.That(await errorBanner.IsVisibleAsync(), Is.True);

        // Tap error banner or container to recover
        await errorBanner.ClickAsync();
        var termsCheckbox = Page.Locator("#termsofservice");
        Assert.That(await termsCheckbox.IsCheckedAsync(), Is.True);

        // Proceed to checkout
        await ProceedPastCartAsync(Page, checkoutButton);
        Assert.That(Page.Url, Does.Not.EndWith("/cart"));
    }
}

