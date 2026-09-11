using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mercurius.Mobile.Models;

// One line of the in-progress sale on SalesPage. Lives only in memory until checkout — nothing
// here is persisted until the cashier taps Charge, at which point SalesPage turns the whole cart
// into a LocalSale + LocalSaleItem rows in one shot.
//
// Quantity raises PropertyChanged so SalesPage.xaml.cs's stepper handlers (Increment/Decrement,
// and re-tapping a tile already in the cart) can update the CollectionView cell in place instead
// of tearing down and rebuilding the whole cart CollectionView on every tap (RenderCart still does
// that full reset for actual add/remove-line changes, where the item count itself changes).
public class CartLine : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid ProductSyncId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public decimal Stock { get; set; }

    private decimal _quantity;
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity == value) return;
            _quantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(QuantityDisplay));
            OnPropertyChanged(nameof(LineTotal));
            OnPropertyChanged(nameof(LineTotalDisplay));
        }
    }

    public decimal LineTotal => Price * Quantity;
    public string PriceDisplay => $"₱{Price:N2}";
    public string LineTotalDisplay => $"₱{LineTotal:N2}";
    public string QuantityDisplay => Quantity.ToString("0.##");

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
