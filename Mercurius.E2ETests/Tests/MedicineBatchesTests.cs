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

    private async Task OpenBatchesForProductAsync(string productName)
    {
        await Page.GotoAsync("/Products");
        // DataTables' search box only reacts to a real 'keyup' event; Fill() doesn't dispatch one.
        await Page.Locator("#searchInput").PressSequentiallyAsync(productName, new LocatorPressSequentiallyOptions { Delay = 20 });
        var batchesLink = Page.Locator($"#productsTable tbody tr:has-text('{productName}') a[title='Batches']");
        await batchesLink.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await batchesLink.ClickAsync();
        await Page.Locator("#batchesTable").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task AddBatch_AppearsInBatchesList()
    {
        await LoginAsAdminAsync();
        var productName = await Page.CreateTestProductAsync("E2E Batch Product");
        await OpenBatchesForProductAsync(productName);

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
