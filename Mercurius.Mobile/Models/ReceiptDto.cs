using System.Text.Json.Serialization;

namespace Mercurius.Mobile.Models;

// Mirrors Mercurius/Models/Sync/SyncDtos.cs's ReceiptSyncDto / ReceiptLineSyncDto /
// ReceiptPullPage — the shape returned by GET api/sync/receipts/pull.
public class ReceiptLineDto
{
    [JsonPropertyName("syncId")] public Guid SyncId { get; set; }
    [JsonPropertyName("productSyncId")] public Guid ProductSyncId { get; set; }
    [JsonPropertyName("productName")] public string ProductName { get; set; } = "";
    [JsonPropertyName("quantity")] public decimal Quantity { get; set; }
    [JsonPropertyName("salePrice")] public decimal SalePrice { get; set; }
    [JsonPropertyName("lineTotal")] public decimal LineTotal { get; set; }
    [JsonPropertyName("refundedQuantity")] public decimal RefundedQuantity { get; set; }
    [JsonPropertyName("vatType")] public int VatType { get; set; }
    [JsonPropertyName("vatAmount")] public decimal VatAmount { get; set; }
    [JsonPropertyName("discountAmount")] public decimal DiscountAmount { get; set; }
}

public class ReceiptDto
{
    [JsonPropertyName("syncId")] public Guid SyncId { get; set; }
    [JsonPropertyName("invoiceNumber")] public string InvoiceNumber { get; set; } = "";
    [JsonPropertyName("invoiceDate")] public DateTime InvoiceDate { get; set; }
    [JsonPropertyName("locationId")] public int LocationId { get; set; }
    [JsonPropertyName("locationName")] public string? LocationName { get; set; }
    [JsonPropertyName("cashierName")] public string? CashierName { get; set; }
    [JsonPropertyName("sourceChannel")] public string? SourceChannel { get; set; }
    [JsonPropertyName("sourceDeviceId")] public string? SourceDeviceId { get; set; }
    [JsonPropertyName("sourceDeviceName")] public string? SourceDeviceName { get; set; }
    [JsonPropertyName("notes")] public string? Notes { get; set; }
    [JsonPropertyName("paymentMethod")] public string? PaymentMethod { get; set; }
    [JsonPropertyName("paidAmount")] public decimal PaidAmount { get; set; }
    [JsonPropertyName("grandTotal")] public decimal GrandTotal { get; set; }
    [JsonPropertyName("refundedAmount")] public decimal RefundedAmount { get; set; }
    [JsonPropertyName("hasRefund")] public bool HasRefund { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("lastModifiedUtc")] public DateTime LastModifiedUtc { get; set; }
    // BIR invoice data (absent from older servers, and zero for web or pre-BIR sales).
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
    [JsonPropertyName("items")] public List<ReceiptLineDto> Items { get; set; } = new();
}

public class ReceiptPullPage
{
    [JsonPropertyName("items")] public List<ReceiptDto> Items { get; set; } = new();
    [JsonPropertyName("nextCursor")] public string? NextCursor { get; set; }
    [JsonPropertyName("hasMore")] public bool HasMore { get; set; }
}
