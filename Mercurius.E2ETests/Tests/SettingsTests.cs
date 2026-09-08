using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class SettingsTests : MercuriusTestBase
{
    public SettingsTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task DatabaseBackups_Loads()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/DatabaseBackups");

        var content = await Page.ContentAsync();
        Assert.DoesNotContain("error occurred while processing your request", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SettingsIndex_Loads()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Settings");

        var content = await Page.ContentAsync();
        Assert.DoesNotContain("error occurred while processing your request", content, StringComparison.OrdinalIgnoreCase);
    }
}
