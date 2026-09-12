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
public class SalesTests : MercuriusTestBase
{
    public SalesTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CompleteSale_ViaCardPayment_ClearsCartAndShowsConfirmation()
    {
        await LoginAsAdminAsync();
        var productName = await Page.CreateTestProductAsync("E2E Sale Product");

        await Page.GotoAsync("/Sales/NewSale");
        await Page.Locator("#productSearch").PressSequentiallyAsync(productName, new LocatorPressSequentiallyOptions { Delay = 20 });

        var tile = Page.Locator($".product-tile:has-text('{productName}')").First;
        await tile.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await tile.ClickAsync();

        var chargeBtn = Page.Locator("#chargeBtn");
        await Assertions.Expect(chargeBtn).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = 5000 });
        await chargeBtn.ClickAsync();

        await Page.Locator("#paymentModal.show").WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });

        // Checkout is an ajax POST that stays on the page (no redirect) — before the InvoiceId
        // fix, this request threw an unhandled DbUpdateException (FOREIGN KEY constraint failed)
        // instead of returning the success JSON below. Wait for the response itself rather than a
        // navigation, since there isn't one.
        var response = await Page.RunAndWaitForResponseAsync(
            async () => await Page.ClickAsync("#chargeCardBtn"),
            resp => resp.Url.Contains("/Sales/NewSale") && resp.Request.Method == "POST");
        Assert.True(response.Ok, $"Checkout POST failed: {await response.TextAsync()}");

        // A successful charge hides the payment modal, clears the cart back to its empty state,
        // and shows a confirmation toast — assert all three rather than just the response status,
        // so a JS-side regression (e.g. clearCart() never firing) still fails the test.
        await Page.Locator("#paymentModal.show").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 5000 });
        await Assertions.Expect(Page.Locator("#cartEmpty")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });
        await Assertions.Expect(Page.Locator(".toast-body")).ToContainTextAsync("Sale complete", new LocatorAssertionsToContainTextOptions { Timeout = 5000 });
    }
}
