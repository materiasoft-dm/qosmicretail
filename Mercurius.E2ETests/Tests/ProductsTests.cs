using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class ProductsTests : MercuriusTestBase
{
    public ProductsTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ProductsList_Loads()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Products");

        await Page.Locator("#productsTable").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        // DataTables populates rows via AJAX after initial render — wait for at least one data
        // row rather than just the empty table shell.
        await Page.Locator("#productsTable tbody tr").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task CreateProduct_ThenAppearsInList()
    {
        await LoginAsAdminAsync();
        var uniqueName = await Page.CreateTestProductAsync("E2E Test Product");

        await Page.GotoAsync("/Products");
        // DataTables' search box only reacts to a real 'keyup' event; Fill() sets the value
        // directly without dispatching one, so type it out one keystroke at a time instead.
        await Page.Locator("#searchInput").PressSequentiallyAsync(uniqueName, new LocatorPressSequentiallyOptions { Delay = 20 });
        await Page.Locator($"#productsTable tbody tr:has-text('{uniqueName}')").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    /// <summary>
    /// "Delete" is a soft delete throughout this app (see CLAUDE.md) — the row must still exist
    /// afterward, just flipped to IsActive = false, not removed from the list.
    /// </summary>
    [Fact]
    public async Task DeleteProduct_IsSoftDeleted_StaysInListAsInactive()
    {
        await LoginAsAdminAsync();
        var productName = await Page.CreateTestProductAsync("E2E Delete Product");

        await Page.GotoAsync("/Products");
        await Page.Locator("#searchInput").PressSequentiallyAsync(productName, new LocatorPressSequentiallyOptions { Delay = 20 });
        var row = Page.Locator($"#productsTable tbody tr:has-text('{productName}')");
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await Assertions.Expect(row).ToContainTextAsync("Active");

        await row.Locator("a[title=Delete]").ClickAsync();
        await Page.WaitForURLAsync(url => url.Contains("/Products/Delete/"), new PageWaitForURLOptions { Timeout = 10000 });

        await Page.ClickAsync("input[type=submit][value=Delete]");
        await Page.WaitForURLAsync(url => url.Contains("/Products") && !url.Contains("Delete"), new PageWaitForURLOptions { Timeout = 10000 });

        await Page.Locator("#searchInput").PressSequentiallyAsync(productName, new LocatorPressSequentiallyOptions { Delay = 20 });
        row = Page.Locator($"#productsTable tbody tr:has-text('{productName}')");
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await Assertions.Expect(row).ToContainTextAsync("Inactive");
    }
}
