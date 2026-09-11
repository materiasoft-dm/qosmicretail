using Mercurius.Mobile.UITests.Infrastructure;

namespace Mercurius.Mobile.UITests.Tests;

public class CartTests : MercuriusMobileTestBase
{
    private const string AlphaTile = "ProductTile_E2E-ALPHA-001";
    private const string BetaTile = "ProductTile_E2E-BETA-002";

    public CartTests(AndroidAppFixture fixture) : base(fixture) { }

    private void GoToClearedSalesPage()
    {
        EnsureLoggedIn();
        ClearCart();
        SetText("ProductSearchEntry", "E2E Widget");
        Thread.Sleep(500);
    }

    [Fact]
    public void AddItemToCart_ThenAddMultiple_ThenAddSecondProduct_ThenRemoveOne()
    {
        GoToClearedSalesPage();

        // 1. Add a single item — appears in the ticket with the correct line total.
        Tap(AlphaTile);
        WaitForId("CartLine_E2E Widget Alpha");
        Assert.Equal("1 item", ById("CartCountLabel").Text);
        Assert.Equal("₱25.00", ById("TotalLabel").Text);
        Assert.Equal("₱25.00", ById("CartLineTotal_E2E Widget Alpha").Text);

        // 2. Add multiple — tapping the same tile again increments quantity rather than adding a
        // duplicate line (see SalesPage.xaml.cs's OnProductTileTapped merge-by-product logic).
        // Waiting for the actual expected quantity text, rather than a fixed sleep, is what makes
        // back-to-back mutations here reliable regardless of how long a given render takes.
        Tap(AlphaTile);
        WaitForIdText("Quantity_E2E Widget Alpha", "2");
        Assert.Equal("₱50.00", ById("CartLineTotal_E2E Widget Alpha").Text);
        Assert.Equal("2 items", ById("CartCountLabel").Text);
        Assert.Equal("₱50.00", ById("TotalLabel").Text);

        // The ticket's own "+" stepper does the same thing — bump to 3.
        Tap("Increment_E2E Widget Alpha");
        WaitForIdText("Quantity_E2E Widget Alpha", "3");
        Assert.Equal("₱75.00", ById("CartLineTotal_E2E Widget Alpha").Text);

        // 3. Add a second, different product — a distinct line alongside the first.
        Tap(BetaTile);
        WaitForId("CartLine_E2E Widget Beta");
        Assert.Equal("4 items", ById("CartCountLabel").Text); // 3 Alpha + 1 Beta
        Assert.Equal("₱87.50", ById("TotalLabel").Text); // 75.00 + 12.50

        // 4. Remove the second product entirely via its ticket "✕" — first line untouched.
        Tap("Remove_E2E Widget Beta");
        WaitUntilGone("CartLine_E2E Widget Beta");
        Assert.True(ExistsById("CartLine_E2E Widget Alpha"));
        Assert.Equal("3 items", ById("CartCountLabel").Text);
        Assert.Equal("₱75.00", ById("TotalLabel").Text);

        // Removing Beta's row shifts Alpha's row as the CollectionView's remove animation plays
        // out (RecyclerView default ~300ms) — WaitUntilGone only confirms Beta's element left the
        // tree, not that the layout has finished settling into its final position. Tapping
        // Decrement immediately risked the tap landing during that reflow; a short settle delay
        // here fixed a real, repeatable failure where that tap emptied the entire cart instead of
        // decrementing Alpha (its element handle apparently resolved to a still-relocating view).
        Thread.Sleep(1500);

        // 5. Decrementing down to 1, then again, removes the line entirely (qty never shows 0).
        // Same reflow hazard as above applies between EACH of these rapid taps, not just the first
        // one after a removal — the CollectionView is still settling from the previous change
        // (quantity text update, or the final removal's own layout pass) when the next tap fires.
        // See CLAUDE.md's "Known flaky area" note: this isn't just slow rendering — a stale tap
        // coordinate can land on ClearButton once the Ticket Grid's rows momentarily collapse and
        // reflow, wiping the whole cart instead of decrementing one line. A generous settle delay
        // is a mitigation, not a fix; a real fix means finding why SalesPage.xaml's Ticket Grid
        // rows collapse during a CollectionView item-count transition.
        Tap("Decrement_E2E Widget Alpha");
        WaitForIdText("Quantity_E2E Widget Alpha", "2");
        Thread.Sleep(1500);
        Tap("Decrement_E2E Widget Alpha");
        WaitForIdText("Quantity_E2E Widget Alpha", "1");
        Thread.Sleep(1500);
        Tap("Decrement_E2E Widget Alpha");
        WaitUntilGone("CartLine_E2E Widget Alpha");
        Assert.True(ExistsByText("Ticket is empty"));
        Assert.Equal("0 items", ById("CartCountLabel").Text);
        Assert.Equal("₱0.00", ById("TotalLabel").Text);

        ClearCart();
        SetText("ProductSearchEntry", "");
    }
}
