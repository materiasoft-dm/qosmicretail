using Mercurius.Mobile.UITests.Windows.Infrastructure;

namespace Mercurius.Mobile.UITests.Windows.Tests;

// NOTE: this deliberately does NOT mirror Mercurius.Mobile.UITests' CartTests.cs (Android) —
// that file still targets a per-line Increment_/Decrement_/Remove_ button API SalesPage.xaml no
// longer has. A cart-line tap now opens QuantityPopup (see SalesPage.xaml.cs's OnCartLineTapped
// and QuantityPopup.xaml.cs), so this suite exercises that popup instead. The Android suite needs
// the same update — left alone here since that's outside this suite's scope.
//
// Also NOTE: taps below target ProductPrice_*/ProductStock_*/CartLineTotal_* (nested labels),
// not ProductTile_*/CartLine_* (the item's own root) — the root AutomationId doesn't forward on
// Windows for CollectionView items; see MercuriusMobileWindowsTestBase's CollectionItems comment.
// A click on the nested label still lands inside the same tappable Border/Grid.
public class CartTests : MercuriusMobileWindowsTestBase
{
    private const string AlphaTapTarget = "ProductStock_E2E-ALPHA-001";
    private const string BetaTapTarget = "ProductStock_E2E-BETA-002";

    public CartTests(WindowsAppFixture fixture) : base(fixture) { }

    private void GoToClearedSalesPage()
    {
        EnsureLoggedIn();
        ClearCart();
        SetText("ProductSearchEntry", "E2E Widget");
        Thread.Sleep(500);
    }

    // Opens the QuantityPopup a cart-line tap now shows (see SalesPage.xaml.cs's
    // OnCartLineTapped / QuantityPopup.xaml.cs), nudges it with the +/- buttons, then applies via
    // OK. The popup's minus floors at 1 — it never auto-removes.
    private void SetLineQuantity(string productName, int delta)
    {
        Tap($"CartLineTotal_{productName}");
        WaitForId("QuantityEntry", timeoutSeconds: 10);
        var button = delta >= 0 ? "QtyPlusButton" : "QtyMinusButton";
        for (var i = 0; i < Math.Abs(delta); i++)
        {
            Tap(button);
        }
        Tap("QtyOkButton");
        WaitUntilGone("QuantityEntry");
    }

    private void RemoveLine(string productName)
    {
        Tap($"CartLineTotal_{productName}");
        WaitForId("QtyRemoveButton", timeoutSeconds: 10);
        Tap("QtyRemoveButton");
        WaitUntilGone("QuantityEntry");
    }

    [Fact]
    public void AddItemToCart_ThenAddMultiple_ThenAddSecondProduct_ThenRemoveOne()
    {
        GoToClearedSalesPage();

        // 1. Add a single item — appears in the ticket with the correct line total.
        Tap(AlphaTapTarget);
        WaitForId("CartLineTotal_E2E Widget Alpha");
        Assert.Equal("1 item", ById("CartCountLabel").Text);
        Assert.Equal("₱25.00", ById("TotalLabel").Text);
        Assert.Equal("₱25.00", ById("CartLineTotal_E2E Widget Alpha").Text);

        // 2. Tapping the same tile again increments quantity rather than adding a duplicate line
        // (see SalesPage.xaml.cs's OnProductTileTapped merge-by-product logic).
        Tap(AlphaTapTarget);
        WaitForIdText("Quantity_E2E Widget Alpha", "Qty: 2");
        Assert.Equal("₱50.00", ById("CartLineTotal_E2E Widget Alpha").Text);
        Assert.Equal("2 items", ById("CartCountLabel").Text);
        Assert.Equal("₱50.00", ById("TotalLabel").Text);

        // The QuantityPopup's own + button does the same thing — bump to 3.
        SetLineQuantity("E2E Widget Alpha", delta: 1);
        WaitForIdText("Quantity_E2E Widget Alpha", "Qty: 3");
        Assert.Equal("₱75.00", ById("CartLineTotal_E2E Widget Alpha").Text);

        // 3. Add a second, different product — a distinct line alongside the first.
        Tap(BetaTapTarget);
        WaitForId("CartLineTotal_E2E Widget Beta");
        Assert.Equal("4 items", ById("CartCountLabel").Text); // 3 Alpha + 1 Beta
        Assert.Equal("₱87.50", ById("TotalLabel").Text); // 75.00 + 12.50

        // 4. Remove the second product entirely via the popup's "Remove Item" button — first line
        // untouched.
        RemoveLine("E2E Widget Beta");
        WaitUntilGone("CartLineTotal_E2E Widget Beta");
        Assert.True(ExistsById("CartLineTotal_E2E Widget Alpha"));
        Assert.Equal("3 items", ById("CartCountLabel").Text);
        Assert.Equal("₱75.00", ById("TotalLabel").Text);

        // 5. Decrementing to 1, then removing via the popup, empties the ticket (the popup's minus
        // never drops below 1 — see QuantityPopup.xaml.cs's OnMinusClicked).
        SetLineQuantity("E2E Widget Alpha", delta: -2);
        WaitForIdText("Quantity_E2E Widget Alpha", "Qty: 1");
        RemoveLine("E2E Widget Alpha");
        WaitUntilGone("CartLineTotal_E2E Widget Alpha");
        Assert.True(ExistsByText("Ticket is empty"));
        Assert.Equal("0 items", ById("CartCountLabel").Text);
        Assert.Equal("₱0.00", ById("TotalLabel").Text);

        ClearCart();
        SetText("ProductSearchEntry", "");
    }

    [Fact]
    public void QuantityPopup_Cancel_LeavesLineUnchanged()
    {
        GoToClearedSalesPage();

        Tap(AlphaTapTarget);
        WaitForId("CartLineTotal_E2E Widget Alpha");
        WaitForIdText("Quantity_E2E Widget Alpha", "Qty: 1");

        Tap("CartLineTotal_E2E Widget Alpha");
        WaitForId("QuantityEntry", timeoutSeconds: 10);
        Tap("QtyPlusButton");
        Tap("QtyPlusButton");
        Tap("QtyCancelButton");
        WaitUntilGone("QuantityEntry");

        // The popup's own edits never applied — the line is exactly as it was before it opened.
        Assert.Equal("Qty: 1", ById("Quantity_E2E Widget Alpha").Text);
        Assert.Equal("₱25.00", ById("CartLineTotal_E2E Widget Alpha").Text);

        ClearCart();
        SetText("ProductSearchEntry", "");
    }
}
