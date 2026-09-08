using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Infrastructure;

/// <summary>One shared Chromium instance for the whole test run; each test gets its own
/// BrowserContext (and therefore its own cookies/session) via MercuriusTestBase.</summary>
public class BrowserFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.CloseAsync();
        Playwright.Dispose();
    }
}
