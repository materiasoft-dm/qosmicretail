using SQLite;

namespace Mercurius.Mobile.Data;

// A sale made on this device — written to the local DB the instant checkout completes, so the
// register keeps working with no connection at all. SyncService pushes anything with
// IsSynced == false to api/sync/invoices/push whenever it gets the chance; the server is the
// authority on stock/batch bookkeeping (see SyncController.PushInvoices), so this row and its
// items are never edited again locally once created — only their IsSynced flag changes.
[Table("Sales")]
public class LocalSale
{
    [PrimaryKey]
    public Guid SyncId { get; set; }

    public DateTime InvoiceDate { get; set; }
    public int? CustomerId { get; set; }
    public int LocationId { get; set; }
    public string? Notes { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal GrandTotal { get; set; }

    [Indexed]
    public bool IsSynced { get; set; }

    public DateTime CreatedUtc { get; set; }
}
