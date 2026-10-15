using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class DashboardTests : BlazorTestBase
{
    public DashboardTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Dashboard_LoadsAfterLogin()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("");

        await Assertions.Expect(Page).ToHaveTitleAsync(
            new System.Text.RegularExpressions.Regex("Dashboard", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

        // The KPI strip is driven by the dashboard API, so its presence means the data arrived.
        await Assertions.Expect(Page.Locator(".kpi").First).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });

        var content = await Page.ContentAsync();
        Assert.DoesNotContain("error occurred while processing your request", content, StringComparison.OrdinalIgnoreCase);
        await AssertNoBlazorErrorAsync();
    }
}
