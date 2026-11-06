using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Nop.Tests.E2E.Fixtures;
using Nop.Tests.E2E.Flows;
using NUnit.Framework;

namespace Nop.Tests.E2E.Tests;

[TestFixture]
[Category("CheckoutE2E")]
[Category("ProductAttributes")]
public class ProductAttributePriceTests : CheckoutTestBase
{
    private const int ProductId = 187;
    private const string ProductSlug = "chamomile-and-calendula-face-and-body-balm";
    private const string AttributeMappingId = "40";
    private const string SmallValueId = "139";
    private const string LargeValueId = "140";
    private const decimal BasePrice = 8.00m;
    private const decimal LargePrice = 15.00m;

    private async Task DismissCookieNotificationAsync(IPage page)
    {
        await page.EvaluateAsync("document.getElementById('eu-cookie-bar-notification')?.remove();");
    }

    private async Task NavigateToProductPageAsync(IPage page)
    {
        var productUrl = $"{Settings.BaseUrl.TrimEnd('/')}/{ProductSlug}";
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigating to product page: {productUrl}");
        await page.GotoAsync(productUrl, new() { WaitUntil = WaitUntilState.Load });
        await DismissCookieNotificationAsync(page);
    }

    private async Task SelectProductSizeAsync(IPage page, string sizeValueId)
    {
        var sizeSelect = page.Locator($"#product_attribute_{AttributeMappingId}, select[data-attr='{AttributeMappingId}']").First;
        await sizeSelect.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Selecting size attribute option value '{sizeValueId}'");
        await sizeSelect.SelectOptionAsync(sizeValueId);

        // Wait for AJAX price calculation to complete and update the DOM
        await Task.Delay(1500);
    }

    private async Task<string> GetDisplayedPriceAsync(IPage page)
    {
        var priceLocator = page.Locator($"#price-value-{ProductId}, .price-value-{ProductId}").First;
        await priceLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        var text = (await priceLocator.InnerTextAsync()).Trim();
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Displayed product price: {text}");
        return text;
    }

    private async Task AddCurrentProductToCartAsync(IPage page)
    {
        var addToCartButton = page.Locator($"#add-to-cart-button-{ProductId}, .add-to-cart-button, button[id^='add-to-cart-button']").First;
        await addToCartButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Clicking Add to Cart for product {ProductId}");
        await addToCartButton.ClickAsync();

        // Allow AJAX cart update notification to process
        await Task.Delay(2500);
    }

    private async Task NavigateToCartAsync(IPage page)
    {
        var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigating to cart: {cartUrl}");
        await page.GotoAsync(cartUrl, new() { WaitUntil = WaitUntilState.Load });
        await DismissCookieNotificationAsync(page);
    }

    private async Task ClearCartIfNotEmptyAsync(IPage page)
    {
        await NavigateToCartAsync(page);
        var removeButtons = page.Locator("button.remove-btn, td.remove-from-cart input[type='checkbox']");
        var count = await removeButtons.CountAsync();
        if (count > 0)
        {
            TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Cleaning up {count} existing cart item(s)...");
            var checkboxes = page.Locator("input[name='removefromcart']");
            var chkCount = await checkboxes.CountAsync();
            for (var i = 0; i < chkCount; i++)
            {
                await checkboxes.Nth(i).CheckAsync(new() { Force = true });
            }

            var updateCartBtn = page.Locator("#updatecart, button[name='updatecart']").First;
            if (await updateCartBtn.IsVisibleAsync())
            {
                await updateCartBtn.ClickAsync();
                await Task.Delay(1500);
            }
        }
    }

    private async Task LoginAsAdminAsync(IPage page)
    {
        var loginUrl = $"{Settings.BaseUrl.TrimEnd('/')}/login";
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Logging into Admin as {Settings.Customer.ReturningCustomerEmail}");
        await page.GotoAsync(loginUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await DismissCookieNotificationAsync(page);

        var logoutLink = page.Locator("a[href*='/logout'], .ico-logout");
        if (!page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) && await logoutLink.CountAsync() > 0)
        {
            await page.GotoAsync($"{Settings.BaseUrl.TrimEnd('/')}/logout", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.GotoAsync(loginUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await DismissCookieNotificationAsync(page);
        }

        var emailInput = page.Locator("#Email, input[name='Email'], input.email").First;
        await emailInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await emailInput.FillAsync(Settings.Customer.ReturningCustomerEmail);

        var passwordInput = page.Locator("#Password, input[name='Password'], input.password").First;
        await passwordInput.FillAsync(Settings.Customer.ReturningCustomerPassword);

        var loginButton = page.Locator("button.login-btn-modern, button[type='submit']:has-text('Log in'), input[value='Log in'], .returning-customer-block button[type='submit']").First;
        await loginButton.ClickAsync();

        await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    [Test]
    [Order(1)]
    [Description("Verifies that selecting size attribute options (Small vs Large) dynamically updates the displayed price via AJAX without full page reload")]
    public async Task ProductPage_Selecting_Size_Attribute_Dynamically_Updates_Price()
    {
        await NavigateToProductPageAsync(Page);

        // 1. Initial pre-selected size is Small, verifying base price (£8.00)
        var initialPrice = await GetDisplayedPriceAsync(Page);
        Assert.That(initialPrice, Does.Contain("8.00"), $"Expected base price £8.00 for pre-selected Small size, but got: {initialPrice}");

        // 2. Select Large size (+£7.00 price adjustment)
        await SelectProductSizeAsync(Page, LargeValueId);

        // Wait for dynamic AJAX price to reflect £15.00
        var largePriceLocator = Page.Locator($"#price-value-{ProductId}:has-text('15.00'), .price-value-{ProductId}:has-text('15.00')");
        await largePriceLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var largePrice = await GetDisplayedPriceAsync(Page);
        Assert.That(largePrice, Does.Contain("15.00"), $"Expected dynamically updated price £15.00 for Large size, but got: {largePrice}");

        // 3. Switch back to Small size (£0.00 price adjustment)
        await SelectProductSizeAsync(Page, SmallValueId);

        var smallPriceLocator = Page.Locator($"#price-value-{ProductId}:has-text('8.00'), .price-value-{ProductId}:has-text('8.00')");
        await smallPriceLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var returnedPrice = await GetDisplayedPriceAsync(Page);
        Assert.That(returnedPrice, Does.Contain("8.00"), $"Expected price to revert to £8.00 when Small is re-selected, but got: {returnedPrice}");
    }

    [Test]
    [Order(2)]
    [Description("Verifies that adding a product with Large size attribute to cart correctly computes the adjusted unit price (£15.00) and displays the selected attribute")]
    public async Task Cart_Adding_Large_Size_Attribute_Applies_Price_Adjustment_In_Cart()
    {
        await ClearCartIfNotEmptyAsync(Page);

        // Navigate to product and select Large size (+£7.00)
        await NavigateToProductPageAsync(Page);
        await SelectProductSizeAsync(Page, LargeValueId);
        await AddCurrentProductToCartAsync(Page);

        // Navigate to cart
        await NavigateToCartAsync(Page);

        var cartTable = Page.Locator("table.cart");
        await cartTable.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        var cartRow = Page.Locator("table.cart tbody tr").First;
        var productName = await cartRow.Locator(".product-name").InnerTextAsync();
        var attributeInfo = await cartRow.Locator(".attributes").InnerTextAsync();
        var unitPrice = await cartRow.Locator(".product-unit-price").InnerTextAsync();
        var subTotal = await cartRow.Locator(".product-subtotal").InnerTextAsync();

        TestContext.WriteLine($"Cart Item: Product='{productName}', Attributes='{attributeInfo}', UnitPrice='{unitPrice}', SubTotal='{subTotal}'");

        Assert.Multiple(() =>
        {
            Assert.That(productName, Does.Contain("Chamomile and Calendula Face and Body Balm").IgnoreCase, "Product name mismatch in cart.");
            Assert.That(attributeInfo, Does.Contain("Large"), "Expected selected size 'Large' in cart item attributes description.");
            Assert.That(unitPrice, Does.Contain("15.00"), "Expected unit price to be £15.00 (Base £8.00 + £7.00 Large size adjustment).");
            Assert.That(subTotal, Does.Contain("15.00"), "Expected item subtotal to be £15.00 for quantity 1.");
        });

        // Verify cart order subtotal reflects £15.00
        var orderSubtotal = Page.Locator(".order-subtotal .value-summary").First;
        var orderSubtotalText = await orderSubtotal.InnerTextAsync();
        Assert.That(orderSubtotalText, Does.Contain("15.00"), $"Expected cart order subtotal of £15.00, got: {orderSubtotalText}");
    }

    [Test]
    [Order(3)]
    [Description("Verifies that adding both Small (£8.00) and Large (£15.00) sizes creates separate line items with accurate unit prices and combined subtotal (£23.00)")]
    public async Task Cart_Adding_Both_Sizes_Creates_Distinct_Line_Items_With_Correct_Subtotal()
    {
        await ClearCartIfNotEmptyAsync(Page);

        // 1. Add Small size (£8.00)
        await NavigateToProductPageAsync(Page);
        await SelectProductSizeAsync(Page, SmallValueId);
        await AddCurrentProductToCartAsync(Page);

        // 2. Add Large size (£15.00)
        await NavigateToProductPageAsync(Page);
        await SelectProductSizeAsync(Page, LargeValueId);
        await AddCurrentProductToCartAsync(Page);

        // 3. Inspect Cart
        await NavigateToCartAsync(Page);

        var cartRows = Page.Locator("table.cart tbody tr");
        var rowCount = await cartRows.CountAsync();
        Assert.That(rowCount, Is.EqualTo(2), "Expected exactly 2 distinct line items in cart for different size attributes.");

        var allCartText = await cartRows.AllInnerTextsAsync();
        TestContext.WriteLine($"Cart items summary:\n{string.Join("\n---\n", allCartText)}");

        var smallItemRow = cartRows.Filter(new() { HasText = "Small" }).First;
        var smallUnitPrice = await smallItemRow.Locator(".product-unit-price").InnerTextAsync();
        var smallSubtotal = await smallItemRow.Locator(".product-subtotal").InnerTextAsync();

        var largeItemRow = cartRows.Filter(new() { HasText = "Large" }).First;
        var largeUnitPrice = await largeItemRow.Locator(".product-unit-price").InnerTextAsync();
        var largeSubtotal = await largeItemRow.Locator(".product-subtotal").InnerTextAsync();

        Assert.Multiple(() =>
        {
            Assert.That(smallUnitPrice, Does.Contain("8.00"), "Small size unit price should be £8.00.");
            Assert.That(smallSubtotal, Does.Contain("8.00"), "Small size subtotal should be £8.00.");
            Assert.That(largeUnitPrice, Does.Contain("15.00"), "Large size unit price should be £15.00.");
            Assert.That(largeSubtotal, Does.Contain("15.00"), "Large size subtotal should be £15.00.");
        });

        // Combined subtotal should be £23.00 (£8.00 + £15.00)
        var orderSubtotal = Page.Locator(".order-subtotal .value-summary").First;
        var orderSubtotalText = await orderSubtotal.InnerTextAsync();
        Assert.That(orderSubtotalText, Does.Contain("23.00"), $"Expected combined cart subtotal of £23.00, got: {orderSubtotalText}");
    }

    [Test]
    [Order(4)]
    [Description("Verifies that increasing the quantity of a price-adjusted size attribute correctly recalculates line and cart totals (2 x £15.00 = £30.00)")]
    public async Task Cart_Quantity_Change_On_Adjusted_Size_Recalculates_Totals()
    {
        await ClearCartIfNotEmptyAsync(Page);

        // Add Large size (£15.00)
        await NavigateToProductPageAsync(Page);
        await SelectProductSizeAsync(Page, LargeValueId);
        await AddCurrentProductToCartAsync(Page);

        await NavigateToCartAsync(Page);

        var cartRow = Page.Locator("table.cart tbody tr").First;
        await cartRow.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        // Click quantity-up arrow button which triggers the change event
        var qtyUpBtn = cartRow.Locator(".quantity.up, [id^='quantity-up-']").First;
        if (await qtyUpBtn.IsVisibleAsync())
        {
            await qtyUpBtn.ClickAsync();
        }
        else
        {
            var qtyInput = cartRow.Locator("input.qty-input, input[name^='itemquantity']");
            await qtyInput.FillAsync("2");
            await qtyInput.DispatchEventAsync("change");
            var updateCartBtn = Page.Locator("#updatecart, button[name='updatecart']").First;
            if (await updateCartBtn.IsVisibleAsync())
            {
                await updateCartBtn.ClickAsync();
            }
        }

        // Wait for subtotal to update to £30.00
        var updatedSubtotalLocator = Page.Locator(".order-subtotal .value-summary:has-text('30.00')").First;
        await updatedSubtotalLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var updatedRow = Page.Locator("table.cart tbody tr").First;
        var unitPrice = await updatedRow.Locator(".product-unit-price").InnerTextAsync();
        var subTotal = await updatedRow.Locator(".product-subtotal").InnerTextAsync();

        Assert.Multiple(() =>
        {
            Assert.That(unitPrice, Does.Contain("15.00"), "Unit price should remain £15.00 after quantity increase.");
            Assert.That(subTotal, Does.Contain("30.00"), "Item subtotal should update to £30.00 (2 x £15.00).");
        });

        var orderSubtotal = Page.Locator(".order-subtotal .value-summary").First;
        var orderSubtotalText = await orderSubtotal.InnerTextAsync();
        Assert.That(orderSubtotalText, Does.Contain("30.00"), $"Expected updated cart order subtotal of £30.00, got: {orderSubtotalText}");
    }

    [Test]
    [Order(5)]
    [Description("Verifies that an administrator can view the size attribute mapping and price adjustments in the Admin Product Edit panel")]
    public async Task Admin_Product_Edit_Displays_Size_Attributes_And_Price_Adjustments()
    {
        await LoginAsAdminAsync(Page);

        // 1. Navigate directly to Product 187 Admin Edit page (from user screenshot)
        var adminProductUrl = $"{Settings.BaseUrl.TrimEnd('/')}/Admin/Product/Edit/{ProductId}";
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigating to Admin Product Edit: {adminProductUrl}");
        await Page.GotoAsync(adminProductUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await DismissCookieNotificationAsync(Page);

        // Verify product name
        var nameInput = Page.Locator("#Name");
        await nameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        var productName = await nameInput.InputValueAsync();
        Assert.That(productName, Does.Contain("Chamomile and Calendula Face and Body Balm"), "Expected product name to match ID 187.");

        // 2. Navigate directly to edit the attribute mapping (Mapping 40)
        var mappingEditUrl = $"{Settings.BaseUrl.TrimEnd('/')}/Admin/Product/ProductAttributeMappingEdit/{AttributeMappingId}";
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Navigating to Product Attribute Mapping Edit: {mappingEditUrl}");
        await Page.GotoAsync(mappingEditUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await DismissCookieNotificationAsync(Page);

        // 3. Wait for DataTables AJAX grid to populate values
        var valuesGrid = Page.Locator("#productattributevalues-grid");
        await valuesGrid.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var largeRow = Page.Locator("#productattributevalues-grid td:has-text('Large')").First;
        await largeRow.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        var valuesTableText = await valuesGrid.InnerTextAsync();
        TestContext.WriteLine($"Attribute Values Grid Text:\n{valuesTableText}");

        Assert.Multiple(() =>
        {
            Assert.That(valuesTableText, Does.Contain("Small"), "Attribute values grid should contain 'Small'.");
            Assert.That(valuesTableText, Does.Contain("Large"), "Attribute values grid should contain 'Large'.");
            Assert.That(valuesTableText, Does.Contain("7"), "Attribute values grid should display the price adjustment of 7 for Large.");
        });
    }

    [Test]
    [Order(6)]
    [Description("Runs end-to-end checkout with a Large size price-adjusted item (£15.00) and verifies the order completes with the correct price and attributes")]
    public async Task Checkout_With_Price_Adjusted_Size_Attribute_Places_Order_Successfully()
    {
        await ClearCartIfNotEmptyAsync(Page);

        // 1. Add Large size (£15.00)
        await NavigateToProductPageAsync(Page);
        await SelectProductSizeAsync(Page, LargeValueId);
        await AddCurrentProductToCartAsync(Page);

        // 2. Go to Cart and verify Large size item with adjusted price
        await NavigateToCartAsync(Page);
        var cartRow = Page.Locator("table.cart tbody tr").First;
        await cartRow.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        var attributeInfo = await cartRow.Locator(".attributes").InnerTextAsync();
        var unitPrice = await cartRow.Locator(".product-unit-price").InnerTextAsync();

        Assert.That(attributeInfo, Does.Contain("Large"), "Cart should contain Large size attribute.");
        Assert.That(unitPrice, Does.Contain("15.00"), "Cart should show £15.00 adjusted unit price.");

        // 3. Complete checkout through standard manual checkout flow
        var flow = new ManualSandboxCheckoutFlow(Settings, CheckoutAuthMode.Guest);
        var result = await flow.CompleteCheckoutFromCartAsync(Page);

        if (!result.Success && result.ErrorMessage != null && result.ErrorMessage.Contains("No offline payment method", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore(result.ErrorMessage);
        }

        TestContext.WriteLine($"Checkout Result: Success={result.Success}, OrderNumber={result.OrderNumber}, Duration={result.Duration}");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, $"Checkout failed: {result.ErrorMessage}");
            Assert.That(result.OrderNumber, Is.Not.Null.And.Not.Empty, "Expected non-empty order number on completion page.");
        });
    }
}
