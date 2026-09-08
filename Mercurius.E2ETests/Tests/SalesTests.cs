using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

/// <summary>
/// Covers the actual checkout flow end to end. This is the exact path that had a silent
/// FOREIGN KEY violation on every single sale (InvoiceItem.InvoiceId was read before
/// SaveChangesAsync ever ran) until it was fixed alongside the FIFO batch pricing feature — see
/// CLAUDE.md. Nothing in the previous test suite ever drove a real checkout, which is how that
/// bug went unnoticed. Don't remove or weaken this test without a good reason.
/// </summary>
public class SalesTests : MercuriusTestBase
{
    public SalesTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    private async Task<string> CreateTestProductAsync()
    {
        var uniqueCode = $"E2ESALE-{Guid.NewGuid():N}".Substring(0, 16);
        var uniqueName = $"E2E Sale Product {Guid.NewGuid():N}".Substring(0, 25);

        await Page.GotoAsync("/Products");
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

        return uniqueName;
    }

    [Fact]
    public async Task CompleteSale_SucceedsAndRedirectsToInvoiceList()
    {
        await LoginAsAdminAsync();
        var productName = await CreateTestProductAsync();

        await Page.GotoAsync("/Sales/NewSale");
        await Page.ClickAsync("#openProductModal");
        await Page.Locator("#productModal.show").WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });

        await Page.FillAsync("#modalProductSearch", productName);
        var addButton = Page.Locator(".modal-add-btn").First;
        await addButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await addButton.ClickAsync();

        await Page.ClickAsync("#productModal .btn-secondary:has-text('Done')");

        var submitBtn = Page.Locator("#submitBtn");
        await Assertions.Expect(submitBtn).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
        await submitBtn.ClickAsync();

        // SalesController.NewSale redirects to InvoiceList on success. Before the InvoiceId fix,
        // this POST threw an unhandled DbUpdateException (FOREIGN KEY constraint failed) instead.
        await Page.WaitForURLAsync(url => url.Contains("/Invoices") || url.Contains("/InvoiceList"), new PageWaitForURLOptions { Timeout = 15000 });

        Assert.DoesNotContain("error occurred while processing your request", await Page.ContentAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
