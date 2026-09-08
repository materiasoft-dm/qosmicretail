using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class PurchaseOrderTests : MercuriusTestBase
{
    public PurchaseOrderTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    private async Task<string> CreateTestSupplierAsync()
    {
        var name = $"E2E PO Supplier {Guid.NewGuid():N}".Substring(0, 30);
        await Page.GotoAsync("/Suppliers/Create");
        await Page.FillAsync("#Name", name);
        await Page.ClickAsync("input[type=submit][value=Create]");
        await Page.WaitForURLAsync(url => url.Contains("/Suppliers") && !url.Contains("Create"), new PageWaitForURLOptions { Timeout = 10000 });
        return name;
    }

    private async Task<string> CreateTestProductAsync()
    {
        var uniqueCode = $"E2EPO-{Guid.NewGuid():N}".Substring(0, 14);
        var uniqueName = $"E2E PO Product {Guid.NewGuid():N}".Substring(0, 25);

        await Page.GotoAsync("/Products");
        await Page.ClickAsync("button[data-bs-target='#createProductModal']");
        await Page.Locator("#createProductModal.show").WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await Page.FillAsync("#Create_Name", uniqueName);
        await Page.FillAsync("#Create_ProductCode", uniqueCode);
        await Page.FillAsync("#Create_CurrentCostPrice", "5.00");
        await Page.FillAsync("#Create_CurrentSalePrice", "9.99");
        await Page.FillAsync("#Create_LowStockCount", "5");
        await Page.FillAsync("#Create_MarkUpPercentage", "0");

        // We're already on /Products, so a URL-contains wait would resolve before the ajax-form's
        // fetch actually happens — wait for the POST's own response instead.
        await Page.RunAndWaitForResponseAsync(
            async () => await Page.ClickAsync("#createProductModal button:has-text('Save Product')"),
            resp => resp.Url.Contains("/Products/Create") && resp.Request.Method == "POST",
            new PageRunAndWaitForResponseOptions { Timeout = 10000 });
        await Page.WaitForLoadStateAsync(LoadState.Load);

        return uniqueName;
    }

    [Fact]
    public async Task PurchaseOrdersList_Loads()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/PurchaseOrders");
        await Page.Locator("#poTable").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task CreatePurchaseOrder_ThenAppearsInListAsPendingApproval()
    {
        await LoginAsAdminAsync();
        var supplierName = await CreateTestSupplierAsync();
        var productName = await CreateTestProductAsync();

        await Page.GotoAsync("/PurchaseOrders/Create");

        // The supplier dropdown is populated from ViewBag.SupplierId via asp-items — select by
        // the exact name we just created rather than a fixed index, since other tests/PO runs
        // may have already added suppliers ahead of it.
        await Page.SelectOptionAsync("select[name=SupplierId]", new SelectOptionValue { Label = supplierName });

        // The product dropdown's option text also carries the live stock count
        // ("Name (Code) — Stock: N"), which we don't know ahead of time, so look up the option's
        // value by a partial text match instead of selecting by label.
        var productOptionValue = await Page.Locator($"select[name=productIds] option:has-text('{productName}')").GetAttributeAsync("value");
        Assert.False(string.IsNullOrEmpty(productOptionValue), $"Could not find a product option containing '{productName}' in the dropdown.");
        await Page.SelectOptionAsync("select[name=productIds]", productOptionValue!);

        await Page.FillAsync("input[name=quantities]", "25");
        await Page.FillAsync("input[name=costs]", "4.50");

        await Page.ClickAsync("button:has-text('Save Purchase Order')");

        // Plain full-page POST (not an ajax-form) redirecting from /PurchaseOrders/Create to
        // /PurchaseOrders — a real, distinguishing URL change, unlike the Products create modal.
        await Page.WaitForURLAsync(url => url.Contains("/PurchaseOrders") && !url.Contains("Create"), new PageWaitForURLOptions { Timeout = 10000 });

        var row = Page.Locator($"#poTable tbody tr:has-text('{supplierName}')");
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await Assertions.Expect(row).ToContainTextAsync("PendingApproval");
    }
}
