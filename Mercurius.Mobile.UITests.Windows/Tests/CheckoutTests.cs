using System.Globalization;
using Mercurius.Mobile.UITests.Windows.Infrastructure;

namespace Mercurius.Mobile.UITests.Windows.Tests;

// Targets E2E Widget Alpha (E2E-ALPHA-001), the same fixture product the rest of this suite uses —
// this file originally targeted a real catalog product ("Amoxicillin 250mg Capsule" / AMOX-250)
// that only exists on the LIVE server. Once this suite standardized on a local dev server (see
// CLAUDE.md's Windows E2E section) for controlled test data, that product no longer existed there
// at all, and every test here started timing out waiting for a tile that could never appear.
public class CheckoutTests : MercuriusMobileWindowsTestBase
{
    private const string ProductCode = "E2E-ALPHA-001";
    private const string ProductName = "E2E Widget Alpha";

    // Nested label, not the tile's own root (ProductTile_E2E-ALPHA-001) — a CollectionView item's
    // root loses its AutomationId on Windows in this MAUI version; see CollectionItems' comment on
    // MercuriusMobileWindowsTestBase. A click here still lands inside the tile's tappable Border.
    private const string Tile = "ProductStock_" + ProductCode;

    public CheckoutTests(WindowsAppFixture fixture) : base(fixture) { }

    private static decimal ParseMoney(string text) =>
        decimal.Parse(text.TrimStart('₱'), NumberStyles.Number, CultureInfo.InvariantCulture);

    [Fact]
    public void CompleteCashSale_ExactAmount_ClearsTicketAndCreatesReceipt()
    {
        EnsureLoggedIn();
        ClearCart();
        SetText("ProductSearchEntry", "E2E Widget Alpha");
        Thread.Sleep(500);

        var stockBeforeText = WaitForId("ProductStock_" + ProductCode).Text;

        // 1. Sell one unit.
        Tap(Tile);
        WaitForIdText("Quantity_" + ProductName, "Qty: 1");
        var total = ParseMoney(ById("TotalLabel").Text);

        Tap("ChargeButton");

        // 2. PaymentPage opens modally — pay the exact total via the Cash flow rather than
        // guessing which quick-amount button (QuickAmount_{n}) got generated for this price.
        var totalDue = WaitForId("TotalDueLabel", timeoutSeconds: 15);
        Assert.Equal($"₱{total:N2}", totalDue.Text);

        SetText("CashReceivedEntry", total.ToString("0.00", CultureInfo.InvariantCulture));
        WaitForIdText("ChangeLabel", "Exact amount", timeoutSeconds: 10);
        Tap("ChargeCashButton");

        // 3. The printed invoice (ReceiptPopup) confirms the charge — dismiss it.
        WaitForId("ReceiptDoneButton", timeoutSeconds: 15).Click();

        // 4. Back on the Sell page: the ticket is cleared and the tile's local stock reflects the
        // sale immediately (SalesPage.xaml.cs reloads products right after SaveSaleAsync).
        WaitForId("ProductSearchEntry", timeoutSeconds: 15);
        Assert.True(ExistsByText("Ticket is empty"));
        SetText("ProductSearchEntry", "E2E Widget Alpha");
        Thread.Sleep(500);
        var stockAfterText = WaitForId("ProductStock_" + ProductCode).Text;
        Assert.NotEqual(stockBeforeText, stockAfterText); // sale decremented local stock display

        // 5. The sale queues locally and syncs best-effort right after checkout.
        Thread.Sleep(2500);
        Assert.Equal("All sales synced", WaitForId("PendingSyncLabel").Text);

        SetText("ProductSearchEntry", "");
    }

    [Fact]
    public void PaymentPage_BackButton_ReturnsToSellPageWithTicketIntact()
    {
        EnsureLoggedIn();
        ClearCart();
        SetText("ProductSearchEntry", "E2E Widget Alpha");
        Thread.Sleep(500);

        Tap(Tile);
        WaitForIdText("Quantity_" + ProductName, "Qty: 1");

        Tap("ChargeButton");
        WaitForId("TotalDueLabel", timeoutSeconds: 15);

        Tap("PaymentBackButton");

        // Cancelling payment doesn't lose the ticket — the cashier can resume or edit it.
        WaitForId("ProductSearchEntry", timeoutSeconds: 15);
        Assert.True(ExistsById("CartLineTotal_" + ProductName));
        Assert.Equal("1", ById("Quantity_" + ProductName).Text.Replace("Qty: ", ""));

        ClearCart();
        SetText("ProductSearchEntry", "");
    }
}
