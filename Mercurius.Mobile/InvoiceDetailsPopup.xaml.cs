using CommunityToolkit.Maui.Views;
using Mercurius.Mobile.Models;
using Mercurius.Shared.Bir;

namespace Mercurius.Mobile;

// Applies one of the tenant's discount types (Settings → Discount Types on the web, synced to
// this device) and/or the buyer's details to the ticket. Returns the new InvoiceDetails via
// Close(...), or null if the cashier cancels.
public partial class InvoiceDetailsPopup : Popup
{
    // Index 0 is "No discount"; the rest are the synced discount types, in the server's order.
    private readonly List<DiscountRule?> _options;

    public InvoiceDetailsPopup(InvoiceDetails current, IReadOnlyList<DiscountRule> discountTypes)
    {
        InitializeComponent();
        _options = new List<DiscountRule?> { null };
        _options.AddRange(discountTypes);
        // A ticket already carrying a discount that has since been deactivated keeps it.
        if (current.Discount != null && !discountTypes.Any(d => d.Id == current.Discount.Id)) _options.Add(current.Discount);
        DiscountPicker.ItemsSource = _options
            .Select(o => o == null ? "No discount" : $"{o.Name} ({o.ValueText}{(o.IsVatExempt ? ", VAT-exempt" : "")})")
            .ToList();

        DiscountPicker.SelectedIndex = Math.Max(0, _options.FindIndex(o => o != null && o.Id == current.Discount?.Id));
        NoDiscountTypesLabel.IsVisible = discountTypes.Count == 0;
        DiscountIdEntry.Text = current.DiscountIdNumber;
        DiscountNameEntry.Text = current.DiscountHolderName;
        DiscountTinEntry.Text = current.DiscountHolderTin;
        BuyerNameEntry.Text = current.BuyerName;
        BuyerTinEntry.Text = current.BuyerTin;
        BuyerAddressEntry.Text = current.BuyerAddress;
        BuyerStyleEntry.Text = current.BuyerBusinessStyle;
        ShowDiscountFields();
    }

    private DiscountRule? SelectedRule =>
        DiscountPicker.SelectedIndex > 0 && DiscountPicker.SelectedIndex < _options.Count ? _options[DiscountPicker.SelectedIndex] : null;

    private void OnDiscountChanged(object? sender, EventArgs e) => ShowDiscountFields();

    private void ShowDiscountFields()
    {
        var rule = SelectedRule;
        DiscountFields.IsVisible = rule?.RequiresCustomerId == true;
        DiscountTinEntry.IsVisible = rule?.AsksForTin == true;
        DiscountIdEntry.Placeholder = rule?.IdPrompt ?? "ID number";
        DiscountHintLabel.Text = rule == null
            ? "Applies to the whole ticket. Ring up items the discount doesn't cover as a separate sale."
            : (rule.IsVatExempt ? $"The 12% VAT is removed first, then {rule.ValueText} is taken off." : $"{rule.ValueText} is taken off the ticket.")
              + (rule.RequiresCustomerId ? " Check the ID before applying." : "");
    }

    private void OnApplyClicked(object? sender, EventArgs e)
    {
        var rule = SelectedRule;
        var asksForId = rule?.RequiresCustomerId == true;
        if (asksForId && (string.IsNullOrWhiteSpace(DiscountIdEntry.Text) || string.IsNullOrWhiteSpace(DiscountNameEntry.Text)))
        {
            ErrorLabel.Text = "Enter the ID number and the name on the ID. Both print on the invoice.";
            ErrorLabel.IsVisible = true;
            return;
        }

        Close(new InvoiceDetails
        {
            Discount = rule,
            DiscountIdNumber = asksForId ? Clean(DiscountIdEntry.Text) : null,
            DiscountHolderName = asksForId ? Clean(DiscountNameEntry.Text) : null,
            DiscountHolderTin = asksForId && rule!.AsksForTin ? Clean(DiscountTinEntry.Text) : null,
            BuyerName = Clean(BuyerNameEntry.Text),
            BuyerTin = Clean(BuyerTinEntry.Text),
            BuyerAddress = Clean(BuyerAddressEntry.Text),
            BuyerBusinessStyle = Clean(BuyerStyleEntry.Text)
        });
    }

    private void OnCancelClicked(object? sender, EventArgs e) => Close(null);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
