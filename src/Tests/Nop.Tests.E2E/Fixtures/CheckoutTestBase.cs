using Microsoft.Playwright;
using Nop.Tests.E2E.Configuration;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Nop.Tests.E2E.Fixtures;

[TestFixture]
public abstract class CheckoutTestBase
{
    protected CheckoutTestSettings Settings { get; private set; } = null!;
    protected IPlaywright PlaywrightInstance { get; private set; } = null!;
    protected IBrowser Browser { get; private set; } = null!;
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Settings = TestConfig.Load();
    }

    [SetUp]
    public async Task SetUp()
    {
        PlaywrightInstance = await Playwright.CreateAsync();

        Browser = await PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = Settings.Headless,
            SlowMo = Settings.SlowMo
        });

        Context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true, // Essential for https://localhost dev certificates
            ViewportSize = new() { Width = 1920, Height = 1080 },
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        });

        // Ensure EU cookie notification banner never blocks clicks in tests (using textContent for Trusted Types compliance)
        await Context.AddInitScriptAsync("(() => { const s = document.createElement('style'); s.textContent = '#eu-cookie-bar-notification { display: none !important; }'; if (document.head) document.head.appendChild(s); else document.addEventListener('DOMContentLoaded', () => document.head.appendChild(s)); })();");

        Page = await Context.NewPageAsync();
        Page.Console += (_, msg) => TestContext.WriteLine($"[BROWSER CONSOLE] {msg.Type}: {msg.Text}");
        Page.PageError += (_, err) => TestContext.WriteLine($"[BROWSER ERROR] {err}");
    }

    [TearDown]
    public async Task TearDown()
    {
        try
        {
            if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
            {
                var screenshotsDir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
                Directory.CreateDirectory(screenshotsDir);

                var testName = TestContext.CurrentContext.Test.Name;
                var fileName = $"{testName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
                var filePath = Path.Combine(screenshotsDir, fileName);

                var activePage = Context.Pages.Count > 0 ? Context.Pages[^1] : Page;
                TestContext.WriteLine($"[FAILURE CURRENT URL] {activePage.Url}");
                await activePage.ScreenshotAsync(new() { Path = filePath, FullPage = true });

                TestContext.WriteLine($"[SCREENSHOT SAVED ON FAILURE] {filePath}");
                TestContext.AddTestAttachment(filePath, "Failure Screenshot");
            }

            // Clear shopping cart after capturing any failure screenshots
            try
            {
                var cartUrl = $"{Settings.BaseUrl.TrimEnd('/')}/cart";
                await Page.GotoAsync(cartUrl, new() { Timeout = 8000, WaitUntil = WaitUntilState.Commit });
                var removeBtns = Page.Locator(".remove-btn, .remove-from-cart button, .remove-from-cart input[type='checkbox']");
                while (await removeBtns.CountAsync() > 0)
                {
                    var first = removeBtns.First;
                    if (await first.IsVisibleAsync())
                    {
                        await first.ClickAsync();
                        await Task.Delay(500);
                    }
                    else break;
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Failed to capture teardown screenshot: {ex.Message}");
        }
        finally
        {
            if (Context != null)
                await Context.CloseAsync();

            if (Browser != null)
                await Browser.CloseAsync();

            PlaywrightInstance?.Dispose();
        }
    }
}
