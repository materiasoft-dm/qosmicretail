using SQLite;

namespace Mercurius.Mobile.Data;

// A Z-reading taken on this terminal (ReadingsPage). Kept here until the server confirms it,
// then kept anyway: it's a small row and the terminal's own history of daily closes.
[Table("ZReadings")]
public class LocalZReading
{
    [PrimaryKey]
    public Guid SyncId { get; set; }

    public int TerminalId { get; set; }
    [Indexed]
    public int ZCounter { get; set; }
    public int ResetCounter { get; set; }
    public DateTime ReadingUtc { get; set; }
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
    public string ReportText { get; set; } = "";

    [Indexed]
    public bool IsSynced { get; set; }

    [Ignore]
    public string Title => $"Z-{ZCounter:0000}  ·  {ReadingUtc.ToLocalTime():MMM d, yyyy h:mm tt}";
    [Ignore]
    public string Subtitle => $"{SalesCount} sale{(SalesCount == 1 ? "" : "s")}  ·  ₱{NetSales:N2}{(IsSynced ? "" : "  ·  not uploaded yet")}";
}
