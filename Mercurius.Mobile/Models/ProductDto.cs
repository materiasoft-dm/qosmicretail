using System.Text.Json.Serialization;

namespace Mercurius.Mobile.Models;

// Mirrors Mercurius/Models/Sync/SyncDtos.cs's ProductSyncDto on the server.
public class ProductDto
{
    [JsonPropertyName("syncId")]
    public Guid SyncId { get; set; }

    [JsonPropertyName("productCode")]
    public string ProductCode { get; set; } = "";

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

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("lastModifiedUtc")]
    public DateTime LastModifiedUtc { get; set; }

    // View-only helpers for the CollectionView's item template.
    public bool IsLowStock => CurrentStock <= LowStockCount;
    public string PriceDisplay => CurrentSalePrice.HasValue ? $"₱{CurrentSalePrice:N2}" : "—";
    public string StockDisplay => $"{CurrentStock:N0} in stock";
}
