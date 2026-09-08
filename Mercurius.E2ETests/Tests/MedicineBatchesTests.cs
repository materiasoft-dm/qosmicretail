using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

/// <summary>
/// Covers the FIFO batch pricing feature's UI: adding a batch and seeing the product's price
/// follow it. The actual FIFO selection logic (oldest batch that can cover a sale, falling
/// through to the newer one otherwise) is exercised by SalesTests / the unit tests — this class
/// just checks the batch-management screen itself works.
/// </summary>
public class MedicineBatchesTests : MercuriusTestBase
{
    public MedicineBatchesTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    private async Task<string> CreateTestProductAndOpenBatchesAsync()
    {
        var uniqueCode = $"E2EBATCH-{Guid.NewGuid():N}".Substring(0, 16);
        var uniqueName = $"E2E Batch Product {Guid.NewGuid():N}".Substring(0, 25);

        await Page.GotoAsync("/Products");
        await Page.ClickAsync("button[data-bs-target='#createProductModal']");
        await Page.Locator("#createProductModal.show").WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await Page.FillAsync("#Create_Name", uniqueName);
        await Page.FillAsync("#Create_ProductCode", uniqueCode);
        // LowStockCount and MarkUpPercentage are non-nullable decimals — an empty input submits
        // "" and fails model binding outright ("The value '' is invalid"), not just validation.
        await Page.FillAsync("#Create_LowStockCount", "5");
        await Page.FillAsync("#Create_MarkUpPercentage", "0");
        // We're already on /Products, so a URL-contains wait would resolve before the ajax-form's
        // fetch actually happens — wait for the POST's own response instead.
        await Page.RunAndWaitForResponseAsync(
            async () => await Page.ClickAsync("#createProductModal button:has-text('Save Product')"),
            resp => resp.Url.Contains("/Products/Create") && resp.Request.Method == "POST",
            new PageRunAndWaitForResponseOptions { Timeout = 10000 });
        await Page.WaitForLoadStateAsync(LoadState.Load);

        // DataTables' search box only reacts to a real 'keyup' event; Fill() doesn't dispatch one.
        await Page.Locator("#searchInput").PressSequentiallyAsync(uniqueCode, new LocatorPressSequentiallyOptions { Delay = 20 });
        var batchesLink = Page.Locator($"#productsTable tbody tr:has-text('{uniqueCode}') a[title='Batches']");
        await batchesLink.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await batchesLink.ClickAsync();

        await Page.Locator("#batchesTable").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        return uniqueName;
    }

    [Fact]
    public async Task AddBatch_AppearsInBatchesList()
    {
        await LoginAsAdminAsync();
        await CreateTestProductAndOpenBatchesAsync();

        var lotNumber = $"LOT-{Guid.NewGuid():N}".Substring(0, 12);
        await Page.FillAsync("input[name=BatchNumber]", lotNumber);
        await Page.FillAsync("input[name=ExpiryDate]", "2027-01-01");
        await Page.FillAsync("input[name=InitialQuantity]", "50");
        await Page.FillAsync("input[name=UnitCost]", "5.00");
        await Page.FillAsync("input[name=UnitSalePrice]", "9.99");
        await Page.ClickAsync("button:has-text('Add Batch')");

        await Page.Locator($"#batchesTable tbody tr:has-text('{lotNumber}')").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        var content = await Page.ContentAsync();
        Assert.Contains("9.99", content);
    }
}
