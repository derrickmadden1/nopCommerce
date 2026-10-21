using Nop.Tests.E2E.Fixtures;
using Nop.Tests.E2E.Flows;
using NUnit.Framework;

namespace Nop.Tests.E2E.Tests;

[TestFixture]
[Category("CheckoutE2E")]
[Category("ReviewReward")]
public class ReviewRewardTests : CheckoutTestBase
{
    private ReviewRewardFlow _flow = null!;

    [SetUp]
    public void SetUpFlow()
    {
        _flow = new ReviewRewardFlow(Settings);
    }

    [Test]
    [Order(1)]
    [Description("Verifies that an unauthenticated guest visiting a product page sees the prompt to log in or register to earn a review reward coupon.")]
    public async Task Guest_Visitor_Sees_Login_Or_Register_Review_Reward_Prompt_On_Product_Page()
    {
        await _flow.LogoutAsync(Page);
        await _flow.NavigateToTestProductAsync(Page);

        var guestPromptCard = Page.Locator(".review-guest-prompt-card");
        await guestPromptCard.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 10000 });

        var promptText = await guestPromptCard.InnerTextAsync();
        var verificationBoxCount = await Page.Locator(".review-reward-verification-box").CountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(promptText, Does.Contain("Want to write a review & earn a discount reward?").IgnoreCase, "Expected guest prompt heading on product review section.");
            Assert.That(verificationBoxCount, Is.EqualTo(0), "ReviewReward verification box should not be rendered for unauthenticated guests.");
        });
    }

    [Test]
    [Order(2)]
    [Description("Verifies that an administrator can configure Review Reward settings and create/toggle Market Purchase Codes in the Admin panel.")]
    public async Task Admin_Can_Configure_Reward_Settings_And_Manage_Market_Purchase_Codes()
    {
        await _flow.UpdateRewardSettingsInAdminAsync(
            Page,
            rewardAmount: 5.00m,
            usePercentage: false,
            couponPrefix: "RVW-",
            expiryDays: 30);

        var bodyText = await Page.InnerTextAsync("body");
        Assert.That(bodyText, Does.Contain("Review Reward settings saved successfully"), "Expected admin success notification after saving settings.");

        var uniqueMarketCode = $"E2E-MKT-{DateTime.UtcNow:HHmmss}";
        await _flow.CreateMarketCodeInAdminAsync(Page, uniqueMarketCode, validDays: 14);

        var row = Page.Locator($"#market-codes-grid tbody tr:has-text('{uniqueMarketCode}')").First;
        var initialStatus = (await row.Locator("td").Nth(3).InnerTextAsync()).Trim();
        Assert.That(initialStatus, Is.EqualTo("Active"), "Newly created market code should have Active status.");

        var deactivatedStatus = await _flow.ToggleMarketCodeStatusInAdminAsync(Page, uniqueMarketCode);
        Assert.That(deactivatedStatus, Is.EqualTo("Inactive"), "Market code status should toggle to Inactive.");

        var reactivatedStatus = await _flow.ToggleMarketCodeStatusInAdminAsync(Page, uniqueMarketCode);
        Assert.That(reactivatedStatus, Is.EqualTo("Active"), "Market code status should toggle back to Active.");
    }

    [Test]
    [Order(3)]
    [Description("Verifies that a customer with a verified online purchase sees the Verified Online Purchase badge, submits a review, earns a coupon, and applies it in the shopping cart.")]
    public async Task Verified_Online_Buyer_Submits_Review_Earns_Coupon_And_Redeems_Online()
    {
        await _flow.LoginAsync(Page);
        await _flow.NavigateToTestProductAsync(Page);

        // If the returning customer hasn't placed an order for the test product yet, place a quick manual order first
        var verifiedBadge = Page.Locator(".review-reward-verification-box .verified-purchase-badge");
        if (await verifiedBadge.CountAsync() == 0)
        {
            TestContext.WriteLine("Returning customer has not yet ordered test product; placing a Manual order first to establish verified purchase.");
            var manualFlow = new ManualSandboxCheckoutFlow(Settings, CheckoutAuthMode.ReturningCustomer);
            var checkoutResult = await manualFlow.RunAsync(Page);
            Assert.That(checkoutResult.Success, Is.True, $"Pre-requisite order failed: {checkoutResult.ErrorMessage}");

            await _flow.NavigateToTestProductAsync(Page);
        }

        await verifiedBadge.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 10000 });
        var badgeText = await verifiedBadge.InnerTextAsync();
        Assert.That(badgeText, Does.Contain("Verified Online Purchase"), "Expected Verified Online Purchase badge for customer who ordered product.");

        var reviewTitle = $"Verified Online Review {DateTime.UtcNow:HHmmssfff}";
        await _flow.SubmitProductReviewAsync(
            Page,
            title: reviewTitle,
            reviewText: "Great product purchased online! Testing automated review reward coupon generation.",
            rating: 5);

        // Check Admin panel for the newly issued coupon
        var coupons = await _flow.GetCouponsInAdminAsync(Page);
        var customerCoupon = coupons.FirstOrDefault(c =>
            c.CustomerEmail.Equals(Settings.Customer.ReturningCustomerEmail, StringComparison.OrdinalIgnoreCase) &&
            !c.IsRedeemed);

        Assert.That(customerCoupon, Is.Not.Null, "Expected an active unredeemed Review Reward coupon to be issued for the verified online buyer.");
        Assert.That(customerCoupon!.CouponCode, Does.StartWith("RVW-"), "Expected coupon code to start with configured prefix 'RVW-'.");

        TestContext.WriteLine($"Verified online buyer earned coupon: {customerCoupon.CouponCode} ({customerCoupon.DiscountText})");

        // Apply the earned coupon in the shopping cart
        await _flow.ClearCartAndDiscountsAsync(Page);
        var applyResult = await _flow.ApplyCouponInCartAsync(Page, customerCoupon.CouponCode);

        Assert.Multiple(() =>
        {
            Assert.That(applyResult.IsApplied, Is.True, $"Expected coupon '{customerCoupon.CouponCode}' to be applied in cart, but got message: {applyResult.Message}");
            Assert.That(applyResult.AppliedCouponCodes.Any(c => c.Contains(customerCoupon.CouponCode, StringComparison.OrdinalIgnoreCase)), Is.True, "Expected coupon code to appear in applied discounts list.");
        });

        await _flow.ClearCartAndDiscountsAsync(Page);
    }

    [Test]
    [Order(4)]
    [Description("Verifies that a customer without an online purchase sees the Market Stall purchase code input, does not earn a coupon with an invalid code, and earns a coupon with a valid market code.")]
    public async Task Market_Stall_Buyer_Submits_Review_With_Valid_Market_Code_And_Earns_Coupon()
    {
        var (newCustomerEmail, newCustomerPassword) = await _flow.RegisterNewCustomerDirectAsync(Page);
        await _flow.NavigateToTestProductAsync(Page);

        var marketEntryBox = Page.Locator(".review-reward-verification-box .market-code-entry");
        await marketEntryBox.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 10000 });

        var marketBoxText = await marketEntryBox.InnerTextAsync();
        Assert.That(marketBoxText, Does.Contain("Bought this item at a Market Stall?"), "Expected Market Stall purchase code entry box for customer without online order.");

        // 1. Submit review with an INVALID market purchase code -> should NOT grant a reward coupon
        await _flow.SubmitProductReviewAsync(
            Page,
            title: "Market Review With Invalid Code",
            reviewText: "Testing review with an invalid market code.",
            rating: 4,
            marketPurchaseCode: "INVALID-STALL-CODE-999");

        var couponsAfterInvalid = await _flow.GetCouponsInAdminAsync(Page);
        var invalidCouponMatch = couponsAfterInvalid.FirstOrDefault(c =>
            c.CustomerEmail.Equals(newCustomerEmail, StringComparison.OrdinalIgnoreCase));
        Assert.That(invalidCouponMatch, Is.Null, "No reward coupon should be granted when an invalid market purchase code is entered.");

        // 2. Log back in as the new customer and submit a review with the VALID market purchase code ('E2E-MARKET-STALL')
        await _flow.LoginAsync(Page, newCustomerEmail, newCustomerPassword);
        await _flow.SubmitProductReviewAsync(
            Page,
            title: "Market Review With Valid Code",
            reviewText: "Bought this at the market stall! Entering valid market code.",
            rating: 5,
            marketPurchaseCode: "E2E-MARKET-STALL");

        var couponsAfterValid = await _flow.GetCouponsInAdminAsync(Page);
        var validCouponMatch = couponsAfterValid.FirstOrDefault(c =>
            c.CustomerEmail.Equals(newCustomerEmail, StringComparison.OrdinalIgnoreCase));

        Assert.That(validCouponMatch, Is.Not.Null, $"Expected a Review Reward coupon to be granted for {newCustomerEmail} after submitting valid market code 'E2E-MARKET-STALL'.");
        Assert.That(validCouponMatch!.CouponCode, Does.StartWith("RVW-"));
        Assert.That(validCouponMatch.IsRedeemed, Is.False);

        TestContext.WriteLine($"Market stall buyer ({newCustomerEmail}) earned coupon: {validCouponMatch.CouponCode}");
    }

    [Test]
    [Order(5)]
    [Description("Verifies that ReviewRewardRequirementRule blocks applying a second Review Reward coupon to the same shopping cart order.")]
    public async Task Second_Review_Reward_Coupon_Is_Blocked_On_Same_Order_By_Requirement_Rule()
    {
        await _flow.LoginAsync(Page);

        // Ensure we have at least 2 unredeemed Review Reward coupons available
        var coupons = (await _flow.GetCouponsInAdminAsync(Page)).Where(c => !c.IsRedeemed).ToList();
        while (coupons.Count < 2)
        {
            await _flow.SubmitProductReviewAsync(
                Page,
                title: $"Extra Review for Stacking Test {DateTime.UtcNow:HHmmssfff}",
                reviewText: "Generating another review reward coupon to test single-reward-per-order requirement rule.",
                rating: 5);

            coupons = (await _flow.GetCouponsInAdminAsync(Page)).Where(c => !c.IsRedeemed).ToList();
        }

        var firstCoupon = coupons[0].CouponCode;
        var secondCoupon = coupons[1].CouponCode;
        TestContext.WriteLine($"Testing cart stacking rule with Coupon 1 = {firstCoupon}, Coupon 2 = {secondCoupon}");

        await _flow.ClearCartAndDiscountsAsync(Page);

        // Apply first review reward coupon -> should succeed
        var firstResult = await _flow.ApplyCouponInCartAsync(Page, firstCoupon, ensureProductInCart: true);
        Assert.That(firstResult.IsApplied, Is.True, $"First review reward coupon '{firstCoupon}' should apply cleanly, but got: {firstResult.Message}");

        // Attempt to apply second review reward coupon to the same cart -> should be blocked by ReviewRewardRequirementRule
        var secondResult = await _flow.ApplyCouponInCartAsync(Page, secondCoupon, ensureProductInCart: false);

        Assert.Multiple(() =>
        {
            Assert.That(secondResult.IsApplied, Is.False, $"Second review reward coupon '{secondCoupon}' should be rejected when '{firstCoupon}' is already applied.");
            Assert.That(secondResult.Message, Does.Contain("Only one review reward coupon code can be applied per order"), "Expected specific requirement rule error message.");
        });

        await _flow.ClearCartAndDiscountsAsync(Page);
    }

    [Test]
    [Order(6)]
    [Description("Verifies that an admin can mark a Review Reward coupon as redeemed at a market stall and that the deactivated coupon can no longer be used online.")]
    public async Task Admin_Can_Mark_Coupon_Redeemed_At_Market_Stall_And_Prevent_Online_Reuse()
    {
        await _flow.LoginAsync(Page);

        // Generate a fresh coupon via review submission
        await _flow.SubmitProductReviewAsync(
            Page,
            title: $"Market Redemption Test Review {DateTime.UtcNow:HHmmssfff}",
            reviewText: "Testing manual market stall redemption in Admin.",
            rating: 5);

        var coupons = await _flow.GetCouponsInAdminAsync(Page);
        var targetCoupon = coupons.FirstOrDefault(c => !c.IsRedeemed);
        Assert.That(targetCoupon, Is.Not.Null, "Expected an unredeemed coupon to test manual market redemption.");

        TestContext.WriteLine($"Marking coupon {targetCoupon!.CouponCode} as redeemed at Market Stall in Admin...");
        var updatedCoupon = await _flow.MarkCouponRedeemedManuallyInAdminAsync(Page, targetCoupon.CouponCode);

        Assert.Multiple(() =>
        {
            Assert.That(updatedCoupon.IsRedeemed, Is.True, "Coupon should be marked as redeemed.");
            Assert.That(updatedCoupon.StatusText, Does.Contain("Redeemed (Market)"), "Status badge should show 'Redeemed (Market)'.");
        });

        // Verify the manually redeemed coupon can no longer be applied online in the shopping cart
        await _flow.ClearCartAndDiscountsAsync(Page);
        var cartAttempt = await _flow.ApplyCouponInCartAsync(Page, targetCoupon.CouponCode, ensureProductInCart: true);

        Assert.That(cartAttempt.IsApplied, Is.False, $"Coupon '{targetCoupon.CouponCode}' was redeemed at a market stall and deactivated, so online cart redemption must fail.");
        await _flow.ClearCartAndDiscountsAsync(Page);
    }

    [Test]
    [Order(7)]
    [Description("Verifies that when Offer Free Product is enabled, completing a review adds the free product and coupon code directly to the basket, enforces 1 reward per order across multiple reviews, removes the free product when the code is removed, and allows swapping to an alternative review reward code.")]
    public async Task Free_Product_Reward_Is_Auto_Added_To_Basket_And_Controlled_By_Coupon_Code()
    {
        try
        {
            await _flow.LoginAsync(Page);
            await _flow.ClearCartAndDiscountsAsync(Page);

            // 1. Enable OfferFreeProduct in Admin settings
            await _flow.UpdateRewardSettingsInAdminAsync(
                Page,
                rewardAmount: 5.00m,
                usePercentage: false,
                couponPrefix: "RVW-FREE-",
                expiryDays: 30,
                offerFreeProduct: true);

            // 2. Submit first product review -> Free product + first coupon code should be added directly to basket
            await _flow.SubmitProductReviewAsync(
                Page,
                title: $"Free Product Review 1 {DateTime.UtcNow:HHmmssfff}",
                reviewText: "First review to earn a free product added directly to the basket.",
                rating: 5);

            var couponsAfterFirst = (await _flow.GetCouponsInAdminAsync(Page, "RVW-FREE-"))
                .Where(c => !c.IsRedeemed)
                .ToList();
            Assert.That(couponsAfterFirst.Count, Is.GreaterThanOrEqualTo(1), "Expected a Free Product Review Reward coupon to be granted.");
            var firstFreeCoupon = couponsAfterFirst[0].CouponCode;
            Assert.That(couponsAfterFirst[0].DiscountText, Does.StartWith("Free Product:"), "Expected admin coupon grid to show Free Product reward.");

            // 3. Submit second product review -> Second code is issued to account, but basket keeps only the first reward
            await _flow.SubmitProductReviewAsync(
                Page,
                title: $"Free Product Review 2 {DateTime.UtcNow:HHmmssfff}",
                reviewText: "Second review to earn an alternative free product code on the account.",
                rating: 5);

            var couponsAfterSecond = (await _flow.GetCouponsInAdminAsync(Page, "RVW-FREE-"))
                .Where(c => !c.IsRedeemed)
                .ToList();
            Assert.That(couponsAfterSecond.Count, Is.GreaterThanOrEqualTo(2), "Expected two Free Product Review Reward coupons on the customer's account.");
            var secondFreeCoupon = couponsAfterSecond.First(c => !c.CouponCode.Equals(firstFreeCoupon, StringComparison.OrdinalIgnoreCase)).CouponCode;

            // 4. Navigate to shopping cart and verify the free product is in the basket and controlled by the first coupon code
            var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
            await Page.GotoAsync(cartUrl, new() { WaitUntil = Microsoft.Playwright.WaitUntilState.DOMContentLoaded });

            var cartRowsBeforeRemove = await Page.Locator(".cart tbody tr").CountAsync();
            var appliedCodesInCart = await Page.Locator(".coupon-box .applied-discount-code").AllInnerTextsAsync();
            Assert.Multiple(() =>
            {
                Assert.That(cartRowsBeforeRemove, Is.EqualTo(1), "Expected 1 free product row in the basket even after completing two reviews.");
                Assert.That(appliedCodesInCart.Any(c => c.Contains(firstFreeCoupon, StringComparison.OrdinalIgnoreCase)), Is.True, $"Expected first coupon '{firstFreeCoupon}' to be applied in the basket.");
            });

            // 5. Remove the discount code from the basket -> free product should also be removed from the basket
            var removeDiscountBtn = Page.Locator("button.remove-discount-button, button[name^='removediscount-']").First;
            await removeDiscountBtn.ClickAsync();
            await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.DOMContentLoaded);

            var cartRowsAfterRemove = await Page.Locator(".cart tbody tr").CountAsync();
            Assert.That(cartRowsAfterRemove, Is.EqualTo(0), "Removing the Free Product coupon code should remove the free product from the basket.");

            // 6. Apply the alternative review reward coupon code (secondFreeCoupon) in the cart -> free product should be added back with the second code
            var applyAltResult = await _flow.ApplyCouponInCartAsync(Page, secondFreeCoupon, ensureProductInCart: true);
            Assert.Multiple(() =>
            {
                Assert.That(applyAltResult.IsApplied, Is.True, $"Expected alternative coupon '{secondFreeCoupon}' to apply cleanly after removing the first code, but got: {applyAltResult.Message}");
                Assert.That(applyAltResult.AppliedCouponCodes.Any(c => c.Contains(secondFreeCoupon, StringComparison.OrdinalIgnoreCase)), Is.True);
            });
        }
        finally
        {
            await _flow.ClearCartAndDiscountsAsync(Page);
            await _flow.UpdateRewardSettingsInAdminAsync(
                Page,
                rewardAmount: 5.00m,
                usePercentage: false,
                couponPrefix: "RVW-",
                expiryDays: 30,
                offerFreeProduct: false);
        }
    }
}
