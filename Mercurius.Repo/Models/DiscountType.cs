using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

// A discount the cashier can apply to a ticket, defined per tenant (Settings → Discount Types).
// The statutory ones (SC, PWD, ...) are seeded by the SeedDefaultDiscountTypes migration and by
// TenantProvisioningService, but are ordinary editable rows. Mirrors Mercurius.Shared.Bir.DiscountRule.
public class DiscountType : ITenantScoped
{
    [Key]
    public int Id { get; set; }
    public int TenantId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "";
    // Printed on the receipt ("Less: SC Disc 20%").
    [StringLength(10)]
    public string? ShortLabel { get; set; }
    // Mercurius.Shared.Bir.DiscountValueKind: 0 = percent (Value 20 = 20%), 1 = fixed peso amount.
    public int ValueKind { get; set; }
    public decimal Value { get; set; }
    // Removes the 12% VAT before the discount (SC/PWD style) and reports the sale as VAT-exempt.
    public bool IsVatExempt { get; set; }
    // Asks for the beneficiary's ID number and name; the receipt prints them with a signature line.
    public bool RequiresCustomerId { get; set; }
    [StringLength(50)]
    public string? IdLabel { get; set; }
    // Mercurius.Shared.Bir.SpecialDiscountType: the line it's reported on in X/Z readings (0 = other).
    public int Category { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}
