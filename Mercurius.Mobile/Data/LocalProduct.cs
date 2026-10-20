using SQLite;

namespace Mercurius.Mobile.Data;

// The mobile app's own local copy of Product — this is what every screen actually reads from.
// Sync only ever refreshes this table from the server; it's never bypassed for a "live" read, so
// the app works fully offline once it has synced at least once.
[Table("Products")]
public class LocalProduct
{
    [PrimaryKey]
    public Guid SyncId { get; set; }

    [Indexed]
    public string ProductCode { get; set; } = "";

    // Manufacturer barcode — scanning it on the Sell page adds this product to the ticket.
    [Indexed]
    public string? Barcode { get; set; }

    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? CategoryName { get; set; }
    public decimal? CurrentCostPrice { get; set; }
    public decimal? CurrentSalePrice { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal LowStockCount { get; set; }
    public decimal MarkUpPercentage { get; set; }
    public bool IsActive { get; set; }
    // Mercurius.Shared.Bir.VatType: how this product shows on a BIR invoice.
    public int VatType { get; set; }
    public DateTime LastModifiedUtc { get; set; }

    // True for a product created on this device that the server hasn't confirmed yet. Sync pushes
    // these first, and a pull never overwrites one, so an offline-created product can't be
    // clobbered by (or lost to) an incoming refresh.
    public bool IsPendingSync { get; set; }

    // View-only helpers for the Products page's item template.
    [Ignore]
    public bool IsLowStock => CurrentStock <= LowStockCount;
    [Ignore]
    public string PriceDisplay => CurrentSalePrice.HasValue ? $"₱{CurrentSalePrice:N2}" : "—";
    [Ignore]
    public string StockDisplay => $"{CurrentStock:N0} in stock";
}
