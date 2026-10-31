using System.Diagnostics;
using System.Net.Http;
using Microsoft.Playwright;
using Nop.Tests.E2E.Configuration;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Nop.Tests.E2E.Fixtures;

[TestFixture]
public abstract class CheckoutTestBase
{
    private static Process? _managedServerProcess;
    private static readonly SemaphoreSlim _serverLock = new(1, 1);

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
        await EnsureStoreRunningAsync();

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

    private async Task EnsureStoreRunningAsync()
    {
        if (await IsStoreListeningAsync(Settings.BaseUrl))
            return;

        await _serverLock.WaitAsync();
        try
        {
            if (await IsStoreListeningAsync(Settings.BaseUrl))
                return;

            var webProjectDir = FindWebProjectDirectory();
            if (webProjectDir == null)
            {
                TestContext.WriteLine($"[E2E Server] Could not locate Presentation/Nop.Web directory from {AppContext.BaseDirectory}");
                return;
            }

            var uri = new Uri(Settings.BaseUrl);
            var port = uri.Port > 0 ? uri.Port : 58552;
            var dllPath = Path.Combine(webProjectDir, "bin", "Debug", "net10.0", "Nop.Web.dll");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = File.Exists(dllPath)
                    ? $"\"{dllPath}\""
                    : $"run --no-build --project \"{Path.Combine(webProjectDir, "Nop.Web.csproj")}\"",
                WorkingDirectory = webProjectDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.Environment["ASPNETCORE_URLS"] = $"https://localhost:{port};http://localhost:{port + 1}";
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

            TestContext.WriteLine($"[E2E Server] Store not running at {Settings.BaseUrl}. Launching Nop.Web in background...");
            _managedServerProcess = Process.Start(startInfo);

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try
                {
                    if (_managedServerProcess is { HasExited: false })
                        _managedServerProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore exit cleanup errors
                }
            };

            for (var i = 0; i < 45; i++)
            {
                await Task.Delay(2000);
                if (await IsStoreListeningAsync(Settings.BaseUrl))
                {
                    TestContext.WriteLine($"[E2E Server] Store is now healthy and listening at {Settings.BaseUrl}.");
                    return;
                }
            }

            TestContext.WriteLine($"[E2E Server] Warning: Timed out waiting for store to respond at {Settings.BaseUrl}.");
        }
        finally
        {
            _serverLock.Release();
        }
    }

    private static async Task<bool> IsStoreListeningAsync(string baseUrl)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync(baseUrl);
            return (int)response.StatusCode < 500;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindWebProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Presentation", "Nop.Web");
            if (File.Exists(Path.Combine(candidate, "Nop.Web.csproj")))
                return candidate;
            dir = dir.Parent;
        }
        return null;
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
                await Page.GotoAsync(cartUrl, new() { Timeout = 8000, WaitUntil = WaitUntilState.DOMContentLoaded });
                var removeBtns = Page.Locator("button.remove-btn, .remove-from-cart button");
                var maxAttempts = 20;
                while (maxAttempts-- > 0 && await removeBtns.CountAsync() > 0)
                {
                    var first = removeBtns.First;
                    if (await first.IsVisibleAsync())
                    {
                        await first.ClickAsync();
                        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
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
