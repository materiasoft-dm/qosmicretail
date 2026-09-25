using Mercurius.Mobile.Data;

namespace Mercurius.Mobile.Models;

// View-model for one row in ReceiptsPage's list — wraps a LocalSale (from this device or pulled
// from the server) with the display strings/colors its DataTemplate needs.
public class ReceiptRow
{
    public ReceiptRow(LocalSale sale, bool isThisDevice)
    {
        Sale = sale;
        IsThisDevice = isThisDevice;
    }

    public LocalSale Sale { get; }
    public bool IsThisDevice { get; }

    public Guid SyncId => Sale.SyncId;
    public bool IsSynced => Sale.IsSynced;
    public bool IsDeleted => string.Equals(Sale.Status, "Deleted", StringComparison.OrdinalIgnoreCase);
    public bool IsFullyRefunded => string.Equals(Sale.Status, "Refunded", StringComparison.OrdinalIgnoreCase);

    public string TotalDisplay => $"₱{Sale.GrandTotal:N2}";
    public string DateDisplay => Sale.InvoiceDate.ToLocalTime().ToString("MMM d, h:mm tt");
    public string NumberDisplay => !string.IsNullOrEmpty(Sale.InvoiceNumber) ? Sale.InvoiceNumber : "Not yet numbered";

    // Where it was rung up + by whom, e.g. "This device · Maria" / "📱 Front Counter · Juan" / "🌐 Web · Ana".
    public string SourceDisplay
    {
        get
        {
            var where = IsThisDevice ? "This device"
                : string.Equals(Sale.SourceChannel, "MobileApp", StringComparison.OrdinalIgnoreCase)
                    ? $"📱 {(string.IsNullOrWhiteSpace(Sale.SourceDeviceName) ? "Another terminal" : Sale.SourceDeviceName)}"
                : string.Equals(Sale.SourceChannel, "Web", StringComparison.OrdinalIgnoreCase) ? "🌐 Web"
                : "Earlier sale";
            return string.IsNullOrWhiteSpace(Sale.CashierName) ? where : $"{where} · {Sale.CashierName}";
        }
    }

    public string BranchDisplay => string.IsNullOrWhiteSpace(Sale.LocationName) ? "" : Sale.LocationName!;

    // One badge, most important state wins.
    public string StatusText =>
        !IsSynced ? "Pending upload"
        : IsDeleted ? "Voided"
        : IsFullyRefunded ? "Refunded"
        : Sale.HasRefund ? "Partly refunded"
        : "Completed";

    public Color StatusTextColor =>
        !IsSynced ? Color.FromArgb("#C62828")
        : IsDeleted ? Color.FromArgb("#616161")
        : IsFullyRefunded || Sale.HasRefund ? Color.FromArgb("#E65100")
        : Color.FromArgb("#2E7D32");

    public Color StatusBackgroundColor =>
        !IsSynced ? Color.FromArgb("#FBE9E9")
        : IsDeleted ? Color.FromArgb("#EEEEEE")
        : IsFullyRefunded || Sale.HasRefund ? Color.FromArgb("#FFF3E0")
        : Color.FromArgb("#E8F5EA");
}
