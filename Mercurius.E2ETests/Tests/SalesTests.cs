using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

/// <summary>
/// Covers the actual checkout flow end to end on /Sales/NewSale — the tile-grid/ticket UI that
/// mirrors Mercurius.Mobile's SalesPage (see CLAUDE.md's "Mobile selling flow" notes and the
/// mirror-mobile-Sell-page plan). This is the exact path that had a silent FOREIGN KEY violation
/// on every single sale (InvoiceItem.InvoiceId was read before SaveChangesAsync ever ran) until it
/// was fixed alongside the FIFO batch pricing feature. Nothing else in the suite ever drives a
/// real checkout, which is how that bug went unnoticed. Don't remove or weaken this test without a
/// good reason — if the page's markup changes again, update the selectors here rather than
/// deleting the coverage.
/// </summary>
public class SalesTests : BlazorTestBase
{
    public SalesTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CompleteSale_ViaCardPayment_ClearsCartAndShowsConfirmation()
    {
        await LoginAsAdminAsync();
        var productName = await CreateTestProductAsync("E2E Sale Product");

        await GotoAppAsync("sell");
        // The sell page is the tile-grid/ticket UI that mirrors Mercurius.Mobile's SalesPage.
        await Page.Locator("#sellSearch").FillAsync(productName);
        await Page.WaitForTimeoutAsync(600);

        var tile = Page.Locator(".mud-card").Filter(new LocatorFilterOptions { HasTextString = productName }).First;
        await Assertions.Expect(tile).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        await tile.ClickAsync();

        var charge = Page.Locator("article.content button").Filter(new LocatorFilterOptions { HasTextString = "Charge" }).First;
        await Assertions.Expect(charge).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = UiTimeoutMs });
        await charge.ClickAsync();

        var dialog = Page.Locator(".mud-dialog");
        await Assertions.Expect(dialog).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Charge Card" }).ClickAsync();

        // A successful charge closes the dialog, empties the ticket and confirms.
        await Assertions.Expect(dialog).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.GetByText("Ticket is empty")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        var toast = await WaitForToastAsync();
        Assert.Contains("Sale complete.", toast);

        // A paid sale is saved as Completed (it used to be left as "Draft" forever). The newest
        // invoice is the first row of the date-sorted list.
        await GotoAppAsync("invoices");
        await Assertions.Expect(FirstRow).ToContainTextAsync("Completed", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }
}
