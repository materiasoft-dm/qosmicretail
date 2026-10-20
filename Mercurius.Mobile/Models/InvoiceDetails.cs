using Mercurius.Shared.Bir;

namespace Mercurius.Mobile.Models;

// What the cashier adds to the ticket beyond its items (InvoiceDetailsPopup): one of the
// tenant's discount types (and its holder, when the type asks for an ID), and the buyer's details for a VAT-registered customer who needs
// them on the invoice. Lives with the ticket and is cleared with it.
public class InvoiceDetails
{
    public DiscountRule? Discount { get; set; }
    public string? DiscountIdNumber { get; set; }
    public string? DiscountHolderName { get; set; }
    public string? DiscountHolderTin { get; set; }

    public string? BuyerName { get; set; }
    public string? BuyerTin { get; set; }
    public string? BuyerAddress { get; set; }
    public string? BuyerBusinessStyle { get; set; }

    public bool HasDiscount => Discount != null;
    public bool HasBuyer => !string.IsNullOrWhiteSpace(BuyerName) || !string.IsNullOrWhiteSpace(BuyerTin);
    public bool IsEmpty => !HasDiscount && !HasBuyer;
}
