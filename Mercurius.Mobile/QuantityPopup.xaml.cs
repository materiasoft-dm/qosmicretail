using CommunityToolkit.Maui.Views;
using Mercurius.Mobile.Models;

namespace Mercurius.Mobile;

// Result returned by QuantityPopup. Remove=true means the cashier chose to delete the line from
// the ticket; otherwise Quantity holds the value to apply.
public class QuantityPopupResult
{
    public bool Remove { get; init; }
    public decimal Quantity { get; init; }
}

// Modal quantity editor shown when a cashier taps a line in the ticket. Lets them type a value
// directly, nudge it with the − / + buttons, or remove the line entirely. Returns a
// QuantityPopupResult via Close(...), or null if they cancel.
public partial class QuantityPopup : Popup
{
    private readonly CartLine _line;

    public QuantityPopup(CartLine line)
    {
        InitializeComponent();
        _line = line;
        ItemNameLabel.Text = line.Name;
        QuantityEntry.Text = line.Quantity.ToString("0.##");
    }

    private void OnMinusClicked(object? sender, EventArgs e)
    {
        if (decimal.TryParse(QuantityEntry.Text, out var qty) && qty > 1)
        {
            QuantityEntry.Text = (qty - 1).ToString("0.##");
        }
    }

    private void OnPlusClicked(object? sender, EventArgs e)
    {
        if (decimal.TryParse(QuantityEntry.Text, out var qty))
        {
            QuantityEntry.Text = (qty + 1).ToString("0.##");
        }
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        if (decimal.TryParse(QuantityEntry.Text, out var qty) && qty > 0)
        {
            Close(new QuantityPopupResult { Quantity = qty });
        }
        else
        {
            Close(null);
        }
    }

    private void OnRemoveClicked(object? sender, EventArgs e)
    {
        Close(new QuantityPopupResult { Remove = true });
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        Close(null);
    }
}