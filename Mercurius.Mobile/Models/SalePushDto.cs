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
