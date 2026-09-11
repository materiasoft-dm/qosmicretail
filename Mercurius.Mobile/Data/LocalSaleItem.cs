using SQLite;

namespace Mercurius.Mobile.Data;

// One line of a LocalSale. ProductName/ProductCode are denormalized (copied at sale time) purely
// so a synced-sales history screen can render without a join — they're never read back for
// pricing or sync, which always goes by ProductSyncId.
[Table("SaleItems")]
public class LocalSaleItem
{
    [PrimaryKey]
    public Guid SyncId { get; set; }

    [Indexed]
    public Guid SaleSyncId { get; set; }

    public Guid ProductSyncId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
}
