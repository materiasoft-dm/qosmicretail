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

    // Server-computed for pulled sales (wholesale lines can carry a custom total). For lines
    // created here: what the line sold for after VAT exemption/discount, or 0 when that's just
    // Quantity * SalePrice — see EffectiveLineTotal.
    public decimal LineTotal { get; set; }
    public decimal RefundedQuantity { get; set; }

    // BIR: the line's tax class as sold, its VAT, and its share of a special discount.
    public int VatType { get; set; }
    public decimal VatAmount { get; set; }
    public decimal DiscountAmount { get; set; }

    [Ignore]
    // A fully discounted line really is 0, so a discount makes LineTotal authoritative even then.
    public decimal EffectiveLineTotal => LineTotal != 0 || DiscountAmount != 0 ? LineTotal : Quantity * SalePrice;
}
