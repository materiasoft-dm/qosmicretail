using Mercurius.Shared.Bir;
using SQLite;

namespace Mercurius.Mobile.Data;

// The tenant's active discount types (Settings → Discount Types on the web), cached so the Sell
// page can apply them offline. Replaced wholesale on every sync pass.
[Table("DiscountTypes")]
public class LocalDiscountType
{
    [PrimaryKey]
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? ShortLabel { get; set; }
    public int ValueKind { get; set; }
    public decimal Value { get; set; }
    public bool IsVatExempt { get; set; }
    public bool RequiresCustomerId { get; set; }
    public string? IdLabel { get; set; }
    public int Category { get; set; }
    // The server's order (statutory first).
    public int SortOrder { get; set; }

    public DiscountRule ToRule() => new()
    {
        Id = Id,
        Name = Name,
        ShortLabel = ShortLabel,
        ValueKind = (DiscountValueKind)ValueKind,
        Value = Value,
        IsVatExempt = IsVatExempt,
        RequiresCustomerId = RequiresCustomerId,
        IdLabel = IdLabel,
        Category = (SpecialDiscountType)Category
    };
}
