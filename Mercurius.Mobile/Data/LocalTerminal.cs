using SQLite;

namespace Mercurius.Mobile.Data;

// This device as a BIR POS terminal: one row (Id = 1), refreshed from the server on every sync
// (BirService.RefreshTerminalAsync) and kept across logouts, since it describes the machine,
// not the cashier. Its counters are the authority for this terminal: the next invoice serial,
// the Z counter and the accumulating grand total move here first, offline, and the server only
// keeps a copy (taking the higher of the two when they meet).
[Table("Terminal")]
public class LocalTerminal
{
    public const int SingletonId = 1;

    [PrimaryKey]
    public int Id { get; set; } = SingletonId;

    public int TerminalId { get; set; }
    public string? TerminalCode { get; set; }
    public string? Name { get; set; }
    public string? Min { get; set; }
    public string? SerialNumber { get; set; }
    public string? PtuNumber { get; set; }
    public DateTime? PtuDateIssued { get; set; }

    public long LastInvoiceSequence { get; set; }
    public int LastZCounter { get; set; }
    public decimal AccumulatedGrandTotal { get; set; }
    public int ResetCounter { get; set; }
    // The newest invoice's JournalHash; the next invoice's hash covers it.
    public string? LastJournalHash { get; set; }

    public int? LocationId { get; set; }
    public string? SellerRegisteredName { get; set; }
    public string? SellerBusinessName { get; set; }
    public string? SellerAddress { get; set; }
    public string? SellerTin { get; set; }
    public string? SellerBranchCode { get; set; }
    public bool SellerIsVatRegistered { get; set; } = true;

    public string? SupplierName { get; set; }
    public string? SupplierAddress { get; set; }
    public string? SupplierTin { get; set; }
    public string? AccreditationNumber { get; set; }
    public DateTime? AccreditationDateIssued { get; set; }
    public DateTime? AccreditationValidUntil { get; set; }

    public bool CanEditRegistration { get; set; }
    public DateTime? LastRefreshedUtc { get; set; }
}
