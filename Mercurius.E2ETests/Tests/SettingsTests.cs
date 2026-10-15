using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class SettingsTests : BlazorTestBase
{
    public SettingsTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task DatabaseBackups_Loads()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("database-backups");

        // Either the backup table (SQLite) or the "hosted on SQL Server" notice is a correct render;
        // the point is that the page resolves its data and shows no unhandled error.
        await Page.Locator("article.content .mud-table, article.content .mud-alert").First
            .WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });

        var content = await Page.ContentAsync();
        Assert.DoesNotContain("error occurred while processing your request", content, StringComparison.OrdinalIgnoreCase);
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task SettingsIndex_Loads()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("settings");

        // Settings is a grid of links to the other config pages (MudButton with an Href renders as <a>).
        await Assertions.Expect(Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Manage" }).First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });

        var content = await Page.ContentAsync();
        Assert.DoesNotContain("error occurred while processing your request", content, StringComparison.OrdinalIgnoreCase);
        await AssertNoBlazorErrorAsync();
    }
}
