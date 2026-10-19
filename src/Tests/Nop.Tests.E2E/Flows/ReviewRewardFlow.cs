using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Nop.Tests.E2E.Configuration;
using NUnit.Framework;

namespace Nop.Tests.E2E.Flows;

public record GrantedCouponInfo(
    string CustomerEmail,
    string ProductName,
    string CouponCode,
    string DiscountText,
    string StatusText,
    bool IsRedeemed);

public record CartCouponApplicationResult(
    bool IsApplied,
    string Message,
    IReadOnlyList<string> AppliedCouponCodes);

public class ReviewRewardFlow : BaseCheckoutFlow
{
    public ReviewRewardFlow(CheckoutTestSettings? settings = null)
        : base(settings)
    {
    }

    public override string RouteName => "ReviewReward E2E Flow";

    public override Task<CheckoutResult> RunAsync(IPage page, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use specific ReviewRewardFlow methods in ReviewRewardTests.");
    }

    public async Task LoginAsync(IPage page, string? email = null, string? password = null)
    {
        var loginEmail = email ?? Settings.Customer.ReturningCustomerEmail;
        var loginPassword = password ?? Settings.Customer.ReturningCustomerPassword;

        var loginUrl = $"{Settings.BaseUrl.TrimEnd('/')}/login";
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigating to login: {loginUrl} for {loginEmail}");
        await page.GotoAsync(loginUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await DismissCookieNotificationAsync(page);

        // If already logged in as someone else, log out first
        var logoutLink = page.Locator("a[href*='/logout'], .ico-logout");
        if (!page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) && await logoutLink.CountAsync() > 0)
        {
            await page.GotoAsync($"{Settings.BaseUrl.TrimEnd('/')}/logout", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.GotoAsync(loginUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await DismissCookieNotificationAsync(page);
        }

        var emailInput = page.Locator("#Email, input[name='Email'], input.email").First;
        await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await emailInput.FillAsync(loginEmail);

        var passwordInput = page.Locator("#Password, input[name='Password'], input.password").First;
        await passwordInput.FillAsync(loginPassword);

        var loginButton = page.Locator("button.login-btn-modern, button[type='submit']:has-text('Log in'), input[value='Log in'], .returning-customer-block button[type='submit']").First;
        await loginButton.ClickAsync();

        await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Logged in successfully as {loginEmail}");
    }

    public async Task LogoutAsync(IPage page)
    {
        var logoutUrl = $"{Settings.BaseUrl.TrimEnd('/')}/logout";
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Logging out via {logoutUrl}");
        await page.GotoAsync(logoutUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await DismissCookieNotificationAsync(page);
    }

    public async Task<(string Email, string Password)> RegisterNewCustomerDirectAsync(IPage page, string? customEmail = null)
    {
        await LogoutAsync(page);

        var registerUrl = $"{Settings.BaseUrl.TrimEnd('/')}/register";
        await page.GotoAsync(registerUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await DismissCookieNotificationAsync(page);

        var customer = Settings.Customer;
        var regEmail = customEmail ?? $"e2e-rvw-{DateTime.UtcNow:yyyyMMddHHmmssfff}@rosecottagecroft.co.uk";
        var regPassword = "Password123!";

        var firstNameInput = page.Locator("#FirstName");
        await firstNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await firstNameInput.FillAsync(customer.FirstName);

        var lastNameInput = page.Locator("#LastName");
        await lastNameInput.FillAsync(customer.LastName);

        var emailInput = page.Locator("#Email");
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
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Registering new customer: {regEmail}");
        await registerSubmitBtn.ClickAsync();

        await page.WaitForURLAsync(url => !url.EndsWith("/register", StringComparison.OrdinalIgnoreCase), new() { Timeout = 20000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        return (regEmail, regPassword);
    }

    public async Task NavigateToTestProductAsync(IPage page)
    {
        var productUrl = $"{Settings.BaseUrl.TrimEnd('/')}/{Settings.TestProductSlug.TrimStart('/')}";
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigating to product page: {productUrl}");
        await page.GotoAsync(productUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await DismissCookieNotificationAsync(page);
    }

    public async Task SubmitProductReviewAsync(
        IPage page,
        string title,
        string reviewText,
        int rating = 5,
        string? marketPurchaseCode = null)
    {
        await NavigateToTestProductAsync(page);

        var reviewForm = page.Locator("#review-form");
        await reviewForm.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var titleInput = page.Locator("#AddProductReview_Title");
        await titleInput.FillAsync(title);

        var textInput = page.Locator("#AddProductReview_ReviewText");
        await textInput.FillAsync(reviewText);

        var ratingRadio = page.Locator($"#addproductrating_{Math.Clamp(rating, 1, 5)}");
        if (await ratingRadio.CountAsync() > 0)
        {
            await ratingRadio.CheckAsync(new() { Force = true });
        }

        if (marketPurchaseCode != null)
        {
            var marketCodeInput = page.Locator("#MarketPurchaseCode");
            await marketCodeInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await marketCodeInput.FillAsync(marketPurchaseCode);
        }

        var submitBtn = page.Locator("#add-review, button[name='add-review']").First;
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Submitting product review '{title}' (MarketCode='{marketPurchaseCode ?? "<none>"}')");
        await submitBtn.ClickAsync();

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await DismissCookieNotificationAsync(page);
    }

    public async Task NavigateToAdminConfigureAsync(IPage page)
    {
        var adminUrl = $"{Settings.BaseUrl.TrimEnd('/')}/Admin/ReviewRewardAdmin/Configure";
        await page.GotoAsync(adminUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
        {
            await LoginAsync(page, Settings.Customer.ReturningCustomerEmail, Settings.Customer.ReturningCustomerPassword);
            await page.GotoAsync(adminUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        }

        await page.Locator("#market-codes-grid").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await page.Locator("#coupons-grid").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        // Give jQuery loadMarketCodes() and loadCoupons() AJAX calls a moment to populate tables
        await Task.Delay(1000);
    }

    public async Task UpdateRewardSettingsInAdminAsync(
        IPage page,
        decimal rewardAmount,
        bool usePercentage,
        string couponPrefix,
        int expiryDays)
    {
        await NavigateToAdminConfigureAsync(page);

        await page.Locator("#RewardAmount").FillAsync(rewardAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));

        var usePercentageCheckbox = page.Locator("#UsePercentage");
        if (await usePercentageCheckbox.IsCheckedAsync() != usePercentage)
        {
            if (usePercentage)
                await usePercentageCheckbox.CheckAsync(new() { Force = true });
            else
                await usePercentageCheckbox.UncheckAsync(new() { Force = true });
        }

        await page.Locator("#CouponPrefix").FillAsync(couponPrefix);
        await page.Locator("#ExpiryDays").FillAsync(expiryDays.ToString());

        var saveBtn = page.Locator("form[action*='Configure'] button[type='submit'], button:has-text('Save Settings')").First;
        await saveBtn.ClickAsync();

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    public async Task CreateMarketCodeInAdminAsync(IPage page, string code, int validDays = 30)
    {
        await NavigateToAdminConfigureAsync(page);

        await page.Locator("#newMarketCode").FillAsync(code);
        await page.Locator("#newMarketDays").FillAsync(validDays.ToString());
        await page.Locator("#btnCreateMarketCode").ClickAsync();

        var upperCode = code.Trim().ToUpperInvariant();
        var rowLocator = page.Locator($"#market-codes-grid tbody tr:has-text('{upperCode}')");
        await rowLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
    }

    public async Task<string> ToggleMarketCodeStatusInAdminAsync(IPage page, string code)
    {
        var upperCode = code.Trim().ToUpperInvariant();
        var rowLocator = page.Locator($"#market-codes-grid tbody tr:has-text('{upperCode}')").First;
        await rowLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var currentStatus = (await rowLocator.Locator("td").Nth(3).InnerTextAsync()).Trim();
        var toggleBtn = rowLocator.Locator("button").First;
        await toggleBtn.ClickAsync();

        // Wait for status text to flip
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(250);
            var updatedStatus = (await rowLocator.Locator("td").Nth(3).InnerTextAsync()).Trim();
            if (!string.Equals(updatedStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
                return updatedStatus;
        }

        return (await rowLocator.Locator("td").Nth(3).InnerTextAsync()).Trim();
    }

    public async Task<IReadOnlyList<GrantedCouponInfo>> GetCouponsInAdminAsync(IPage page, string? searchCouponCode = null)
    {
        await NavigateToAdminConfigureAsync(page);

        if (searchCouponCode != null)
        {
            await page.Locator("#searchCouponCode").FillAsync(searchCouponCode);
            await page.Locator("#btnSearchCoupon").ClickAsync();
            await Task.Delay(800);
        }

        var rows = page.Locator("#coupons-grid tbody tr");
        var count = await rows.CountAsync();
        var list = new List<GrantedCouponInfo>();

        for (var i = 0; i < count; i++)
        {
            var row = rows.Nth(i);
            var cells = row.Locator("td");
            if (await cells.CountAsync() < 7)
                continue;

            var customerEmail = (await cells.Nth(0).InnerTextAsync()).Trim();
            var productName = (await cells.Nth(1).InnerTextAsync()).Trim();
            var couponCode = (await cells.Nth(2).InnerTextAsync()).Trim();
            var discountText = (await cells.Nth(3).InnerTextAsync()).Trim();
            var statusText = (await cells.Nth(5).InnerTextAsync()).Trim();
            var isRedeemed = statusText.Contains("Redeemed", StringComparison.OrdinalIgnoreCase);

            list.Add(new GrantedCouponInfo(customerEmail, productName, couponCode, discountText, statusText, isRedeemed));
        }

        return list;
    }

    public async Task<GrantedCouponInfo> MarkCouponRedeemedManuallyInAdminAsync(IPage page, string couponCode)
    {
        await NavigateToAdminConfigureAsync(page);

        await page.Locator("#searchCouponCode").FillAsync(couponCode);
        await page.Locator("#btnSearchCoupon").ClickAsync();
        await Task.Delay(800);

        var row = page.Locator($"#coupons-grid tbody tr:has-text('{couponCode}')").First;
        await row.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        void DialogHandler(object? sender, IDialog dialog)
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Accepting admin confirm dialog: {dialog.Message}");
            _ = dialog.AcceptAsync();
        }

        page.Dialog += DialogHandler;
        try
        {
            var redeemBtn = row.Locator("button:has-text('Mark Redeemed')").First;
            await redeemBtn.ClickAsync();

            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(300);
                var statusText = (await row.Locator("td").Nth(5).InnerTextAsync()).Trim();
                if (statusText.Contains("Redeemed", StringComparison.OrdinalIgnoreCase))
                    break;
            }
        }
        finally
        {
            page.Dialog -= DialogHandler;
        }

        var updatedList = await GetCouponsInAdminAsync(page, couponCode);
        return updatedList.First(c => c.CouponCode.Equals(couponCode, StringComparison.OrdinalIgnoreCase));
    }

    public async Task ClearCartAndDiscountsAsync(IPage page)
    {
        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        await page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await DismissCookieNotificationAsync(page);

        // Remove any applied discount coupons first
        var removeDiscountBtns = page.Locator("button.remove-discount-button, button[name^='removediscount-']");
        while (await removeDiscountBtns.CountAsync() > 0)
        {
            var btn = removeDiscountBtns.First;
            if (await btn.IsVisibleAsync())
            {
                await btn.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            }
            else
            {
                break;
            }
        }

        await ClearCartAsync(page);
    }

    public async Task<CartCouponApplicationResult> ApplyCouponInCartAsync(IPage page, string couponCode, bool ensureProductInCart = true)
    {
        if (ensureProductInCart)
        {
            var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
            await page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            var hasItems = await page.Locator(".remove-btn, .remove-from-cart, .cart tbody tr").CountAsync() > 0;
            if (!hasItems)
            {
                await AddTestProductToCartAsync(page);
                await page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            }

            await DismissCookieNotificationAsync(page);

            // Now that at least one item is in the cart, _DiscountBox is rendered; remove any pre-existing coupons
            var removeDiscountBtns = page.Locator("button.remove-discount-button, button[name^='removediscount-']");
            while (await removeDiscountBtns.CountAsync() > 0)
            {
                var btn = removeDiscountBtns.First;
                if (await btn.IsVisibleAsync())
                {
                    await btn.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                }
                else
                {
                    break;
                }
            }
        }

        await DismissCookieNotificationAsync(page);

        var couponInput = page.Locator("#discountcouponcode");
        await couponInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await couponInput.FillAsync(couponCode);

        var applyBtn = page.Locator("#applydiscountcouponcode");
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Applying coupon '{couponCode}' in shopping cart");
        await applyBtn.ClickAsync();

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await DismissCookieNotificationAsync(page);

        var successMsgLocator = page.Locator(".coupon-box .message-success");
        var failureMsgLocator = page.Locator(".coupon-box .message-failure");

        var isApplied = await successMsgLocator.CountAsync() > 0 && await successMsgLocator.First.IsVisibleAsync();
        var message = string.Empty;

        if (isApplied)
        {
            message = (await successMsgLocator.First.InnerTextAsync()).Trim();
        }
        else if (await failureMsgLocator.CountAsync() > 0)
        {
            message = (await failureMsgLocator.First.InnerTextAsync()).Trim();
        }

        var appliedCodeLocators = page.Locator(".coupon-box .applied-discount-code");
        var appliedCount = await appliedCodeLocators.CountAsync();
        var appliedCodes = new List<string>();
        for (var i = 0; i < appliedCount; i++)
        {
            var text = (await appliedCodeLocators.Nth(i).InnerTextAsync()).Trim();
            appliedCodes.Add(text);
        }

        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Cart coupon '{couponCode}' result: IsApplied={isApplied}, Message='{message}', AppliedCount={appliedCodes.Count}");
        return new CartCouponApplicationResult(isApplied, message, appliedCodes);
    }
}
