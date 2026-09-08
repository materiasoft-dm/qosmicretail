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

        // Headless by default (CI-friendly); set E2E_HEADED=1 to watch the browser locally, e.g.:
        //   $env:E2E_HEADED = "1"; dotnet test
        var headed = Environment.GetEnvironmentVariable("E2E_HEADED");
        var isHeaded = headed == "1" || string.Equals(headed, "true", StringComparison.OrdinalIgnoreCase);

        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !isHeaded,
            SlowMo = isHeaded ? 250 : 0 // slow down actions so a human can actually follow along
        });
    }

    public async Task DisposeAsync()
    {
        // Guard against partial initialization (e.g. TestServerFixture failed to start first,
        // in the shared MercuriusCollectionFixture) so cleanup doesn't mask the real error with
        // a NullReferenceException of its own.
        if (Browser != null) await Browser.CloseAsync();
        Playwright?.Dispose();
    }
}
