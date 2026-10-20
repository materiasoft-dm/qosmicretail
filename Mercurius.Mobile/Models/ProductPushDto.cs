using System.Text.Json.Serialization;

namespace Mercurius.Mobile.Models;

// Mirrors Mercurius/Models/Sync/SyncDtos.cs's ProductPushDto on the server — the shape POSTed to
// api/sync/products/push.
public class ProductPushDto
{
    [JsonPropertyName("syncId")]
    public Guid SyncId { get; set; }

    [JsonPropertyName("productCode")]
    public string ProductCode { get; set; } = "";

    [JsonPropertyName("barcode")]
    public string? Barcode { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("currentCostPrice")]
    public decimal? CurrentCostPrice { get; set; }

    [JsonPropertyName("currentSalePrice")]
    public decimal? CurrentSalePrice { get; set; }

    [JsonPropertyName("currentStock")]
    public decimal CurrentStock { get; set; }

    [JsonPropertyName("lowStockCount")]
    public decimal LowStockCount { get; set; }

    [JsonPropertyName("markUpPercentage")]
    public decimal MarkUpPercentage { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("vatType")]
    public int? VatType { get; set; }
}
