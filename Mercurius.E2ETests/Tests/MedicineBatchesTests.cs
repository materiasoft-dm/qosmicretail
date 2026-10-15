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
public class MedicineBatchesTests : BlazorTestBase
{
    public MedicineBatchesTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AddBatch_AppearsInBatchesList()
    {
        await LoginAsAdminAsync();
        var productName = await CreateTestProductAsync("E2E Batch Product");

        // The products grid links straight to the product's batches.
        await GotoAppAsync("products");
        await SearchAsync("Search by name, SKU or barcode...", productName);
        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = productName });
        await Assertions.Expect(row).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await row.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Batches" }).ClickAsync();
        await Assertions.Expect(Page.GetByText($"Batches — {productName}"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });

        var lotNumber = $"LOT-{Guid.NewGuid():N}"[..12];
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add Batch" }).ClickAsync();
        await FillFieldAsync("Batch #", lotNumber);
        // Expiry is a MudDatePicker, whose text box is readonly and only settable through the picker
        // overlay; the form already defaults it to a year out, which is what this test needs.
        await FillFieldAsync("Unit cost", "5.00");
        await FillFieldAsync("Unit sale price", "9.99");
        await FillFieldAsync("Quantity", "50");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).ClickAsync();

        var batchRow = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = lotNumber });
        await Assertions.Expect(batchRow).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(batchRow).ToContainTextAsync("₱9.99");
        await Assertions.Expect(batchRow).ToContainTextAsync("50.0 / 50.0");
        await AssertNoBlazorErrorAsync();
    }
}
