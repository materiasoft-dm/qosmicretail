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
        await Page.GotoAsync("/Products");

        var uniqueCode = $"E2E-{Guid.NewGuid():N}".Substring(0, 12);
        var uniqueName = $"E2E Test Product {Guid.NewGuid():N}".Substring(0, 30);

        await Page.ClickAsync("button[data-bs-target='#createProductModal']");
        await Page.Locator("#createProductModal.show").WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });

        await Page.FillAsync("#Create_Name", uniqueName);
        await Page.FillAsync("#Create_ProductCode", uniqueCode);
        await Page.FillAsync("#Create_CurrentCostPrice", "5.00");
        await Page.FillAsync("#Create_CurrentSalePrice", "9.99");
        // LowStockCount and MarkUpPercentage are non-nullable decimals — an empty input submits
        // "" and fails model binding outright ("The value '' is invalid"), not just validation.
        await Page.FillAsync("#Create_LowStockCount", "5");
        await Page.FillAsync("#Create_MarkUpPercentage", "0");

        // The create form is an ajax-form: the click fires a fetch, and only on success does the
        // JS handler do `window.location.href = redirect`. We're already sitting on /Products, so
        // waiting for a URL "containing /Products" would resolve instantly without ever actually
        // waiting for that fetch — wait for the POST's own response instead, which only arrives
        // once the save genuinely completes, then let the resulting redirect finish loading.
        await Page.RunAndWaitForResponseAsync(
            async () => await Page.ClickAsync("#createProductModal button:has-text('Save Product')"),
            resp => resp.Url.Contains("/Products/Create") && resp.Request.Method == "POST",
            new PageRunAndWaitForResponseOptions { Timeout = 10000 });
        await Page.WaitForLoadStateAsync(LoadState.Load);

        await Page.GotoAsync("/Products");
        // DataTables' search box only reacts to a real 'keyup' event; Fill() sets the value
        // directly without dispatching one, so type it out one keystroke at a time instead.
        await Page.Locator("#searchInput").PressSequentiallyAsync(uniqueCode, new LocatorPressSequentiallyOptions { Delay = 20 });
        await Page.Locator($"#productsTable tbody tr:has-text('{uniqueCode}')").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }
}
