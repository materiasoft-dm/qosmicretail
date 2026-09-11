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

    [JsonPropertyName("items")]
    public List<SaleItemPushDto> Items { get; set; } = new();
}

public class SalePushResult
{
    [JsonPropertyName("syncId")]
    public Guid SyncId { get; set; }

    [JsonPropertyName("created")]
    public bool Created { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
