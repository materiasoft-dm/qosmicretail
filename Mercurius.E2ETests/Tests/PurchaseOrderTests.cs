using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class PurchaseOrderTests : MercuriusTestBase
{
    public PurchaseOrderTests(MercuriusCollectionFixture fixture) : base(fixture) { }

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
        var supplierName = await Page.CreateTestSupplierAsync("E2E PO Supplier");
        var productName = await Page.CreateTestProductAsync("E2E PO Product");

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
