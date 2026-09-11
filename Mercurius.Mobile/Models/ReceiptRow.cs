namespace Mercurius.Mobile.Models;

// View-model for one row in ReceiptsPage's list — wraps a LocalSale with the display strings its
// DataTemplate needs, since LocalSale itself is a plain SQLite row with no formatting helpers.
public class ReceiptRow
{
    public Guid SyncId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal GrandTotal { get; set; }
    public bool IsSynced { get; set; }

    public string TotalDisplay => $"₱{GrandTotal:N2}";
    public string DateDisplay => InvoiceDate.ToLocalTime().ToString("MMM d, h:mm tt");
    public string StatusText => IsSynced ? "Synced" : "Unsynced";

    // LowStockColorConverter treats true as the "warning" state (red) — an unsynced receipt is
    // the thing worth flagging, so this is what the status badge actually binds to.
    public bool NeedsSyncWarning => !IsSynced;
}
