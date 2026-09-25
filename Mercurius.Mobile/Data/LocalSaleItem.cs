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

    // Server-computed for pulled sales (wholesale lines can carry a custom total); 0 for lines
    // created here, where Quantity * SalePrice is the total — see EffectiveLineTotal.
    public decimal LineTotal { get; set; }
    public decimal RefundedQuantity { get; set; }

    [Ignore]
    public decimal EffectiveLineTotal => LineTotal != 0 ? LineTotal : Quantity * SalePrice;
}
