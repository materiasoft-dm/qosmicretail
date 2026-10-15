using CommunityToolkit.Maui.Views;
using Mercurius.Mobile.Models;

namespace Mercurius.Mobile;

// A CommunityToolkit.Maui Popup, not a modal ContentPage — this used to be pushed/popped via
// Navigation.PushModalAsync/PopModalAsync, which fatally crashes the Windows build (confirmed:
// 0xc000027b inside Microsoft.ui.xaml.dll on PopModalAsync, a known unresolved upstream MAUI/WinUI3
// bug — https://github.com/microsoft/microsoft-ui-xaml/issues/10897). QuantityPopup and
// CategoryPickerPopup already used the Popup pattern successfully elsewhere in this app, so this
// follows the same shape instead of trying to work around PopModalAsync itself.
public partial class PaymentPage : Popup
{
    private readonly decimal _totalDue;

    public PaymentPage(decimal totalDue)
    {
        InitializeComponent();
        _totalDue = totalDue;
        TotalDueLabel.Text = $"₱{totalDue:N2}";
        BuildQuickAmounts();
    }

    private void BuildQuickAmounts()
    {
        // Same idea as Loyverse's quick-cash buttons: the exact amount plus a couple of round
        // denominations at or above it, so the cashier can usually tap instead of typing.
        var suggestions = new List<decimal> { _totalDue };
        foreach (var tier in new decimal[] { 20, 50, 100, 500, 1000 })
        {
            if (tier >= _totalDue && !suggestions.Contains(tier))
            {
                suggestions.Add(tier);
            }
        }
        suggestions = suggestions.Distinct().OrderBy(a => a).Take(4).ToList();

        for (var i = 0; i < suggestions.Count; i++)
        {
            var amount = suggestions[i];
            var button = new Button
            {
                Text = $"₱{amount:N2}",
                AutomationId = $"QuickAmount_{amount:0}",
                HeightRequest = 48,
                BackgroundColor = (Color)Application.Current!.Resources["Gray100"],
                TextColor = (Color)Application.Current!.Resources["Gray500"]
            };
            button.Clicked += (_, _) =>
            {
                CashReceivedEntry.Text = amount.ToString("0.##");
            };
            QuickAmountsGrid.Add(button, i % 2, i / 2);
        }
    }

    private void OnCashReceivedChanged(object? sender, TextChangedEventArgs e)
    {
        if (decimal.TryParse(e.NewTextValue, out var received) && received >= _totalDue)
        {
            var change = received - _totalDue;
            ChangeLabel.Text = change > 0 ? $"Change: ₱{change:N2}" : "Exact amount";
            ChargeCashButton.IsEnabled = true;
        }
        else
        {
            ChangeLabel.Text = string.IsNullOrEmpty(e.NewTextValue) ? "" : "Amount is less than the total due";
            ChargeCashButton.IsEnabled = false;
        }
    }

    private void OnChargeCashClicked(object? sender, EventArgs e)
    {
        var received = decimal.TryParse(CashReceivedEntry.Text, out var r) ? r : _totalDue;
        var result = new PaymentResult
        {
            Method = PaymentMethod.Cash,
            AmountReceived = received,
            Change = received - _totalDue
        };
        Close(result);
    }

    private void OnChargeCardClicked(object? sender, EventArgs e)
    {
        var result = new PaymentResult
        {
            Method = PaymentMethod.Card,
            AmountReceived = _totalDue,
            Change = 0
        };
        Close(result);
    }

    private void OnCancelTapped(object? sender, EventArgs e)
    {
        Close(null);
    }
}
