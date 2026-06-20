using Microsoft.Playwright;
using NUnit.Framework;

namespace Mercurius.Tests;

/// <summary>
/// Simple test to verify browser visibility.
/// </summary>
[TestFixture]
public class BrowserVisibilityTest
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    [SetUp]
    public async Task SetUp()
    {
        // Explicitly create Playwright and launch browser with visible window
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        
        // Launch Chromium with headless explicitly set to false
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 500,
            Args = new[] { "--start-maximized" }
        });
        
        // Create context and page
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });
        _page = await _context.NewPageAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        if (_playwright != null) _playwright.Dispose();
    }

    [Test]
    public async Task Browser_Should_Be_Visible()
    {
        Assert.That(_page, Is.Not.Null, "Page should be created");
        
        // Navigate to login page
        await _page.GotoAsync("http://localhost:5094/Identity/Account/Login");
        
        // Bring browser to foreground
        await _page.BringToFrontAsync();
        
        // Wait for page to load
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Check title
        var title = await _page.TitleAsync();
        Console.WriteLine($"Page title: {title}");
        
        Assert.That(title, Does.Contain("Mercurius").Or.Contain("Sign in").Or.Contain("Login"),
            "Page should load correctly");
        
        // Wait so you can see the browser (15 seconds)
        Console.WriteLine("Browser should now be visible. Waiting 15 seconds...");
        await _page.WaitForTimeoutAsync(15000);
        
        // Take a screenshot to verify
        await _page.ScreenshotAsync(new PageScreenshotOptions { Path = "browser_visible.png" });
        Console.WriteLine("Screenshot saved as browser_visible.png");
    }
}