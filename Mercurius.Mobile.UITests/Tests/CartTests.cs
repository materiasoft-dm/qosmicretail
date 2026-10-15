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

    // Opens the QuantityPopup a cart-line tap now shows (see SalesPage.xaml.cs's
    // OnCartLineTapped / QuantityPopup.xaml.cs — this replaced the old per-line
    // Increment_/Decrement_ steppers this test used to drive directly), nudges it with the +/-
    // buttons, then applies via OK. The popup's minus floors at 1 — it never auto-removes.
    private void SetLineQuantity(string productName, int delta)
    {
        Tap($"CartLine_{productName}");
        WaitForId("QuantityEntry", timeoutSeconds: 10);
        var button = delta >= 0 ? "QtyPlusButton" : "QtyMinusButton";
        for (var i = 0; i < Math.Abs(delta); i++)
        {
            Tap(button);
        }
        Tap("QtyOkButton");
        WaitUntilGone("QuantityEntry");
        // Same CollectionView reflow hazard as the old inline steppers (see CLAUDE.md's "Known
        // flaky area" note): the Ticket Grid can momentarily collapse/reflow while the popup's
        // change lands, so a rapid follow-up tap risks landing on a since-relocated element. A
        // settle delay here is a mitigation, not a fix.
        Thread.Sleep(1000);
    }

    private void RemoveLine(string productName)
    {
        Tap($"CartLine_{productName}");
        WaitForId("QtyRemoveButton", timeoutSeconds: 10);
        Tap("QtyRemoveButton");
        WaitUntilGone("QuantityEntry");
        Thread.Sleep(1500); // removal changes the cart's item count, same reflow hazard as above
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
        Tap(AlphaTile);
        WaitForIdText("Quantity_E2E Widget Alpha", "Qty: 2");
        Assert.Equal("₱50.00", ById("CartLineTotal_E2E Widget Alpha").Text);
        Assert.Equal("2 items", ById("CartCountLabel").Text);
        Assert.Equal("₱50.00", ById("TotalLabel").Text);

        // The QuantityPopup's own + button does the same thing — bump to 3.
        SetLineQuantity("E2E Widget Alpha", delta: 1);
        WaitForIdText("Quantity_E2E Widget Alpha", "Qty: 3");
        Assert.Equal("₱75.00", ById("CartLineTotal_E2E Widget Alpha").Text);

        // 3. Add a second, different product — a distinct line alongside the first.
        Tap(BetaTile);
        WaitForId("CartLine_E2E Widget Beta");
        Assert.Equal("4 items", ById("CartCountLabel").Text); // 3 Alpha + 1 Beta
        Assert.Equal("₱87.50", ById("TotalLabel").Text); // 75.00 + 12.50

        // 4. Remove the second product entirely via the popup's "Remove Item" button — first line
        // untouched.
        RemoveLine("E2E Widget Beta");
        WaitUntilGone("CartLine_E2E Widget Beta");
        Assert.True(ExistsById("CartLine_E2E Widget Alpha"));
        Assert.Equal("3 items", ById("CartCountLabel").Text);
        Assert.Equal("₱75.00", ById("TotalLabel").Text);

        // 5. Decrementing to 1, then removing via the popup, empties the ticket.
        SetLineQuantity("E2E Widget Alpha", delta: -2);
        WaitForIdText("Quantity_E2E Widget Alpha", "Qty: 1");
        RemoveLine("E2E Widget Alpha");
        WaitUntilGone("CartLine_E2E Widget Alpha");
        Assert.True(ExistsByText("Ticket is empty"));
        Assert.Equal("0 items", ById("CartCountLabel").Text);
        Assert.Equal("₱0.00", ById("TotalLabel").Text);

        ClearCart();
        SetText("ProductSearchEntry", "");
    }
}
