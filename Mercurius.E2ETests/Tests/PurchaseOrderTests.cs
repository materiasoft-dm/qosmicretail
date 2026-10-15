using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class PurchaseOrderTests : BlazorTestBase
{
    public PurchaseOrderTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PurchaseOrdersList_Loads()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("purchase-orders");
        await Page.Locator("article.content .mud-table").WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>
    /// The list-side counterpart to BlazorPurchaseOrderTests (which asserts the details page): a new
    /// order must show up on the grid as Pending approval, with its supplier and line item.
    /// </summary>
    [Fact]
    public async Task CreatePurchaseOrder_ThenAppearsInListAsPendingApproval()
    {
        await LoginAsAdminAsync();
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var supplierName = await CreateTestSupplierAsync("E2E PO Supplier");
        var productName = await CreateTestProductAsync("E2E PO Product");

        await GotoAppAsync("purchase-orders/new");
        await SelectOptionAsync("Supplier", supplierName);
        await PickAutocompleteAsync(Page.GetByLabel("Product 1"), productName, productName);
        await FillFieldAsync(Page.GetByLabel("Quantity").Nth(0), "25");
        await FillFieldAsync(Page.GetByLabel("Est. unit cost").Nth(0), "4.50");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create purchase order" }).ClickAsync();
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/purchase-orders/\d+$"),
            new PageWaitForURLOptions { Timeout = UiTimeoutMs });

        await GotoAppAsync("purchase-orders");
        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = supplierName });
        await Assertions.Expect(row).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(row).ToContainTextAsync("Pending approval");
        await AssertNoBlazorErrorAsync();
    }
}
