using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

// A Z-reading (end-of-day close) taken on a POS terminal and uploaded by sync. Create-once by
// SyncId, like invoices: it's a record of what the terminal printed, never edited afterwards.
public class PosZReading : ITenantScoped
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Guid SyncId { get; set; }
    public int PosTerminalId { get; set; }
    public int? LocationId { get; set; }

    public int ZCounter { get; set; }
    public int ResetCounter { get; set; }
    // When the reading was taken, as the device saw it (UTC).
    public DateTime ReadingDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }

    public int SalesCount { get; set; }
    public long BeginningInvoiceSequence { get; set; }
    public long EndingInvoiceSequence { get; set; }
    public decimal GrossSales { get; set; }
    public decimal LessVat { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal SeniorCitizenDiscount { get; set; }
    public decimal PwdDiscount { get; set; }
    public decimal SoloParentDiscount { get; set; }
    public decimal NationalAthleteDiscount { get; set; }
    public decimal MedalOfValorDiscount { get; set; }
    public decimal NetSales { get; set; }
    public decimal VatableSales { get; set; }
    public decimal VatAmount { get; set; }
    public decimal VatExemptSales { get; set; }
    public decimal ZeroRatedSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal PreviousGrandTotal { get; set; }
    public decimal NewGrandTotal { get; set; }

    // The reading exactly as printed.
    public string? ReportText { get; set; }

    public virtual PosTerminal? PosTerminal { get; set; }
}
