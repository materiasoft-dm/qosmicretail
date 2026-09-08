using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class SuppliersTests : MercuriusTestBase
{
    public SuppliersTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SuppliersList_Loads()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Suppliers");
        await Page.Locator("#suppliersTable").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task CreateSupplier_ThenAppearsInList()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Suppliers/Create");

        var name = $"E2E Supplier {Guid.NewGuid():N}".Substring(0, 25);
        await Page.FillAsync("#Name", name);
        await Page.ClickAsync("input[type=submit][value=Create]");

        await Page.WaitForURLAsync(url => url.Contains("/Suppliers") && !url.Contains("Create"), new PageWaitForURLOptions { Timeout = 10000 });

        // The search box only reacts to a real 'keyup' event, which Fill() doesn't dispatch.
        await Page.Locator("#searchInput").PressSequentiallyAsync(name, new LocatorPressSequentiallyOptions { Delay = 20 });
        await Page.Locator($"#suppliersTable tbody tr:has-text('{name}')").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }
}
