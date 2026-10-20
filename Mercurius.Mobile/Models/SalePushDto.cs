using System.Text.Json.Serialization;

namespace Mercurius.Mobile.Models;

// Mirrors Mercurius/Models/Sync/SyncDtos.cs's InvoicePushDto/InvoiceItemSyncDto on the server —
// the shape POSTed to api/sync/invoices/push.
public class SaleItemPushDto
{
    [JsonPropertyName("syncId")]
    public Guid SyncId { get; set; }

    [JsonPropertyName("productSyncId")]
    public Guid ProductSyncId { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("salePrice")]
    public decimal SalePrice { get; set; }

    [JsonPropertyName("costPrice")]
    public decimal CostPrice { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    // BIR: as printed on the invoice. NetAmount = what the customer paid for the line.
    [JsonPropertyName("vatType")] public int? VatType { get; set; }
    [JsonPropertyName("vatAmount")] public decimal VatAmount { get; set; }
    [JsonPropertyName("discountAmount")] public decimal DiscountAmount { get; set; }
    [JsonPropertyName("netAmount")] public decimal? NetAmount { get; set; }
}

public class SalePushDto
{
    [JsonPropertyName("syncId")]
    public Guid SyncId { get; set; }

    [JsonPropertyName("invoiceDate")]
    public DateTime InvoiceDate { get; set; }

    [JsonPropertyName("customerId")]
    public int? CustomerId { get; set; }

    [JsonPropertyName("locationId")]
    public int LocationId { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("paidAmount")]
    public decimal PaidAmount { get; set; }

    // "Cash" | "Card". Null for sales queued by an older build; the server then reads Notes.
    [JsonPropertyName("paymentMethod")]
    public string? PaymentMethod { get; set; }

    // Who actually rang the sale up on this device. Usually the same person whose token pushes
    // it — but not after a "log out anyway" with sales still queued, when the next person to
    // sign in uploads them. The server verifies it (same tenant) before trusting it.
    [JsonPropertyName("cashierUserId")]
    public string? CashierUserId { get; set; }

    // ---- BIR invoice, as printed (see LocalSale). Null terminal/sequence = not a BIR invoice.
    [JsonPropertyName("posTerminalId")] public int? PosTerminalId { get; set; }
    [JsonPropertyName("terminalInvoiceSequence")] public long? TerminalInvoiceSequence { get; set; }
    [JsonPropertyName("vatableSales")] public decimal VatableSales { get; set; }
    [JsonPropertyName("vatAmount")] public decimal VatAmount { get; set; }
    [JsonPropertyName("vatExemptSales")] public decimal VatExemptSales { get; set; }
    [JsonPropertyName("zeroRatedSales")] public decimal ZeroRatedSales { get; set; }
    [JsonPropertyName("lessVat")] public decimal LessVat { get; set; }
    [JsonPropertyName("discountAmount")] public decimal DiscountAmount { get; set; }
    [JsonPropertyName("discountTypeId")] public int? DiscountTypeId { get; set; }
    [JsonPropertyName("discountName")] public string? DiscountName { get; set; }
    [JsonPropertyName("specialDiscountType")] public int SpecialDiscountType { get; set; }
    [JsonPropertyName("specialDiscountIdNumber")] public string? SpecialDiscountIdNumber { get; set; }
    [JsonPropertyName("specialDiscountHolderName")] public string? SpecialDiscountHolderName { get; set; }
    [JsonPropertyName("specialDiscountHolderTin")] public string? SpecialDiscountHolderTin { get; set; }
    [JsonPropertyName("buyerName")] public string? BuyerName { get; set; }
    [JsonPropertyName("buyerTin")] public string? BuyerTin { get; set; }
    [JsonPropertyName("buyerAddress")] public string? BuyerAddress { get; set; }
    [JsonPropertyName("buyerBusinessStyle")] public string? BuyerBusinessStyle { get; set; }
    [JsonPropertyName("receiptText")] public string? ReceiptText { get; set; }
    [JsonPropertyName("journalHash")] public string? JournalHash { get; set; }

    [JsonPropertyName("items")]
    public List<SaleItemPushDto> Items { get; set; } = new();
}

// Also the result shape of api/sync/products/push (the server's SyncPushResultItem).
public class SalePushResult
{
    [JsonPropertyName("syncId")]
    public Guid SyncId { get; set; }

    [JsonPropertyName("created")]
    public bool Created { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
