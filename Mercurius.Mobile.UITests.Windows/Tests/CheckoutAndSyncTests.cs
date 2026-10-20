using System.Linq;
using Mercurius.Mobile.UITests.Windows.Infrastructure;

namespace Mercurius.Mobile.UITests.Windows.Tests;

public class CheckoutAndSyncTests : MercuriusMobileWindowsTestBase
{
    // Nested label, not the tile's own root (ProductTile_E2E-ALPHA-001) — a CollectionView item's
    // root loses its AutomationId on Windows in this MAUI version; see CollectionItems' comment on
    // MercuriusMobileWindowsTestBase. A click here still lands inside the tile's tappable Border.
    private const string AlphaTile = "ProductStock_E2E-ALPHA-001";

    public CheckoutAndSyncTests(WindowsAppFixture fixture) : base(fixture) { }

    [Fact]
    public void CompleteCashSale_DeductsStock_CreatesReceipt_AndSyncsToServer()
    {
        EnsureLoggedIn();
        ClearCart();
        SetText("ProductSearchEntry", "E2E Widget Alpha");
        Thread.Sleep(500);

        var stockBefore = WaitForId("ProductStock_E2E-ALPHA-001").Text; // "50 in stock"

        // 1. Sell one unit.
        Tap(AlphaTile);
        Thread.Sleep(300);
        Assert.Equal("₱25.00", ById("TotalLabel").Text);

        Tap("ChargeButton");

        // 2. PaymentPage opens modally — pay exact cash amount via its quick-amount button.
        var totalDue = WaitForId("TotalDueLabel", timeoutSeconds: 10);
        Assert.Equal("₱25.00", totalDue.Text);
        Tap("QuickAmount_25");
        Thread.Sleep(200);
        Tap("ChargeCashButton");

        // 3. The printed invoice (ReceiptPopup) confirms the charge — dismiss it.
        WaitForId("ReceiptDoneButton", timeoutSeconds: 10).Click();

        // 4. Back on the Sell page: the ticket is cleared and the tile's local stock reflects the
        // sale immediately (SalesPage.xaml.cs reloads products right after SaveSaleAsync).
        WaitForId("ProductSearchEntry", timeoutSeconds: 10);
        Assert.True(ExistsByText("Ticket is empty"));
        SetText("ProductSearchEntry", "E2E Widget Alpha");
        Thread.Sleep(500);
        var stockAfter = WaitForId("ProductStock_E2E-ALPHA-001").Text;
        Assert.NotEqual(stockBefore, stockAfter);
        // Not asserting the exact value (e.g. "49") — CheckoutTests.cs sells this same fixture
        // product too, and xunit doesn't guarantee class execution order within a collection, so
        // more than one sale may already have landed by the time this runs. Only the direction
        // (strictly fewer) is a meaningful assertion here.
        var before = int.Parse(new string(stockBefore.TakeWhile(char.IsDigit).ToArray()));
        var after = int.Parse(new string(stockAfter.TakeWhile(char.IsDigit).ToArray()));
        Assert.True(after < before, $"Expected stock to decrease from '{stockBefore}', got '{stockAfter}'.");

        // 5. The sale queues locally and syncs best-effort right after checkout — give it a moment
        // then confirm the header no longer reports anything pending.
        Thread.Sleep(2000);
        Assert.Equal("All sales synced", WaitForId("PendingSyncLabel").Text);

        // 6. Confirm the receipt itself: correct total and a "Synced" status badge.
        NavigateTo("Receipts");
        Assert.Contains("all synced", WaitForId("ReceiptsSummaryLabel", timeoutSeconds: 10).Text, StringComparison.OrdinalIgnoreCase);

        var firstReceiptTotal = WaitForText("₱25.00");
        firstReceiptTotal.Click();
        Thread.Sleep(300);
        Assert.Equal("₱25.00", WaitForId("DetailTotalLabel").Text);
        Assert.Equal("Synced", WaitForId("DetailStatusLabel").Text);

        NavigateTo("Sell");
        WaitForId("ProductSearchEntry", timeoutSeconds: 10);
        SetText("ProductSearchEntry", "");
    }

    [Fact]
    public void SyncNowButton_OnReceiptsPage_CompletesWithoutError()
    {
        EnsureLoggedIn();
        NavigateTo("Receipts");
        WaitForId("SyncNowButton", timeoutSeconds: 10);

        Tap("SyncNowButton");
        Thread.Sleep(1500);

        // No pending count left, and no error alert appeared.
        Assert.Contains("all synced", WaitForId("ReceiptsSummaryLabel").Text, StringComparison.OrdinalIgnoreCase);
        Assert.False(ExistsByText("Sync Failed"));

        NavigateTo("Sell");
        WaitForId("ProductSearchEntry", timeoutSeconds: 10);
    }
}
