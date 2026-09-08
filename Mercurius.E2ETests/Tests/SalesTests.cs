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

    [Fact]
    public async Task CompleteSale_SucceedsAndRedirectsToInvoiceList()
    {
        await LoginAsAdminAsync();
        var productName = await Page.CreateTestProductAsync("E2E Sale Product");

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
