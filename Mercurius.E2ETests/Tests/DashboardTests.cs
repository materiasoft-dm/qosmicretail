using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class DashboardTests : MercuriusTestBase
{
    public DashboardTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Dashboard_LoadsAfterLogin()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/");

        await Assertions.Expect(Page).ToHaveTitleAsync(
            new System.Text.RegularExpressions.Regex("Dashboard", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

        var content = await Page.ContentAsync();
        Assert.DoesNotContain("error occurred while processing your request", content, StringComparison.OrdinalIgnoreCase);
    }
}
