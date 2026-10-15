using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class SuppliersTests : BlazorTestBase
{
    public SuppliersTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SuppliersList_Loads()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("suppliers");
        await Page.Locator("article.content .mud-table").WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task CreateSupplier_ThenAppearsInList()
    {
        await LoginAsAdminAsync();
        var name = await CreateTestSupplierAsync();

        // The grid filters server-side, so find the new row through its own search box.
        await SearchAsync("Search suppliers...", name);
        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = name });
        await Assertions.Expect(row).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(row).ToContainTextAsync("Active");
        await AssertNoBlazorErrorAsync();
    }
}
