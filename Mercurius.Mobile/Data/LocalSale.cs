using SQLite;

namespace Mercurius.Mobile.Data;

// One sale, keyed by its SyncId, whether it was rung up on THIS device or anywhere else (another
// terminal, the web app). One table rather than "my sales" + "everyone's sales" means a sale
// never shows twice: when this device's own sale comes back in a receipts pull, it's the same row.
//
// Two ways a row gets here:
//   - Checkout on this device (IsFromThisDevice = true, IsSynced = false until the server
//     confirms it). Until then the row is the only copy that exists anywhere — it is NEVER
//     overwritten by a pull and NEVER deleted by logout or pruning.
//   - A receipts pull from the server (SyncService.PullReceiptsAsync). The server is the authority
//     for these: every pull overwrites the row wholesale (refunds, status, invoice number...).
//     A pulled copy of one of our own sales also flips IsSynced to true — this is what recovers a
//     push whose response was lost (the server saved it, the device never heard back).
[Table("Sales")]
public class LocalSale
{
    [PrimaryKey]
    public Guid SyncId { get; set; }

    [Indexed]
    public DateTime InvoiceDate { get; set; }
    public int? CustomerId { get; set; }
    public int LocationId { get; set; }
    public string? Notes { get; set; }
    public string? PaymentMethod { get; set; }     // "Cash" | "Card"; null on sales from older builds
    public decimal PaidAmount { get; set; }
    public decimal GrandTotal { get; set; }

    [Indexed]
    public bool IsSynced { get; set; }

    public DateTime CreatedUtc { get; set; }

    // --- Provenance / display (filled at checkout for our own sales, from the server otherwise)
    public bool IsFromThisDevice { get; set; }
    public string? InvoiceNumber { get; set; }     // server-assigned; null until synced
    public string? LocationName { get; set; }
    public string? CashierUserId { get; set; }     // who rang it up here (sent with the push)
    public string? CashierName { get; set; }
    public string? SourceChannel { get; set; }     // "MobileApp" | "Web" | null (older sales)
    public string? SourceDeviceId { get; set; }
    public string? SourceDeviceName { get; set; }

    // --- Server-side state (only ever set from a pull)
    public string? Status { get; set; }            // e.g. Draft, Refunded, Deleted
    public bool HasRefund { get; set; }
    public decimal RefundedAmount { get; set; }
    public DateTime? ServerModifiedUtc { get; set; }
}
