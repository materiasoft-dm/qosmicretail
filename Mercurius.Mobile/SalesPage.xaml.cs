using System.Collections.ObjectModel;
using Mercurius.Mobile.Data;
using Mercurius.Mobile.Models;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class SalesPage : ContentPage
{
    private const string AllCategory = "All";

    private readonly LocalDatabase _localDatabase;
    private readonly SyncService _syncService;

    private List<LocalProduct> _allProducts = new();
    // ObservableCollection so CartCollectionView (bound once, see the constructor) picks up
    // add/remove/clear directly via INotifyCollectionChanged instead of needing ItemsSource torn
    // down and reassigned on every cart mutation. That teardown/rebuild pattern (still used for
    // _allProducts' grid/list views, which only change on a full reload) turned out to be more than
    // a performance smell for the cart specifically: Android's CollectionView recycles the
    // underlying views it tears down, and a rapid successive tap (e.g. the ticket's own Decrement
    // stepper tapped twice in a row) could land on a view mid-recycle and fire a stale handler —
    // observed as an entire well-populated cart suddenly reading empty after a single decrement.
    private readonly ObservableCollection<CartLine> _cart = new();
    private string _selectedCategory = AllCategory;
    // Persisted per-device so the cashier's last choice (tile vs. list) survives an app restart
    // instead of always resetting to the grid default — whichever they're already comfortable
    // with is the one they expect to see again.
    private const string ViewModePreferenceKey = "SalesPage_IsGridView";
    private bool _isGridView = Preferences.Default.Get(ViewModePreferenceKey, true);
    private System.Timers.Timer? _searchDebounceTimer;

    public SalesPage(LocalDatabase localDatabase, SyncService syncService)
    {
        InitializeComponent();
        _localDatabase = localDatabase;
        _syncService = syncService;
        CartCollectionView.ItemsSource = _cart;
        ApplyViewMode();
    }

    private void ApplyViewMode()
    {
        ProductsGridView.IsVisible = _isGridView;
        ProductsListView.IsVisible = !_isGridView;
        ViewToggleLabel.Text = _isGridView ? "▦" : "☰";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProductsAsync();
        await RefreshPendingSyncLabelAsync();
    }

    private async Task LoadProductsAsync()
    {
        _allProducts = await _localDatabase.GetProductsAsync();

        var categories = new List<string> { AllCategory };
        categories.AddRange(_allProducts
            .Select(p => p.CategoryName)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)!);

        CategoriesCollectionView.ItemsSource = categories;
        CategoriesCollectionView.SelectedItem = AllCategory;

        ApplyFilter();
    }

    private async Task RefreshPendingSyncLabelAsync()
    {
        var pending = await _localDatabase.GetPendingSaleCountAsync();
        PendingSyncLabel.Text = pending == 0
            ? "All sales synced"
            : pending == 1
                ? "1 sale pending sync"
                : $"{pending} sales pending sync";
    }

    private void ApplyFilter()
    {
        IEnumerable<LocalProduct> filtered = _allProducts;

        if (_selectedCategory != AllCategory)
        {
            filtered = filtered.Where(p => p.CategoryName == _selectedCategory);
        }

        var search = SearchEntry.Text;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.ProductCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var results = filtered.OrderBy(p => p.Name).ToList();
        ProductsGridView.ItemsSource = results;
        ProductsListView.ItemsSource = results;
        EmptyStateLayout.IsVisible = results.Count == 0;
    }

    private void OnCategorySelected(object? sender, SelectionChangedEventArgs e)
    {
        _selectedCategory = e.CurrentSelection.FirstOrDefault() as string ?? AllCategory;
        ApplyFilter();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer = new System.Timers.Timer(250) { AutoReset = false };
        _searchDebounceTimer.Elapsed += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(ApplyFilter);
        };
        _searchDebounceTimer.Start();
    }

    private void OnViewToggleClicked(object? sender, EventArgs e)
    {
        _isGridView = !_isGridView;
        Preferences.Default.Set(ViewModePreferenceKey, _isGridView);
        ApplyViewMode();
    }

    private void OnProductTileTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not LocalProduct product) return;

        var existing = _cart.FirstOrDefault(c => c.ProductSyncId == product.SyncId);
        if (existing != null)
        {
            // Quantity's setter raises PropertyChanged, so the bound cell updates in place —
            // no need to touch the CollectionView's ItemsSource for a line that already exists.
            existing.Quantity++;
            RefreshCartTotals();
        }
        else
        {
            // ObservableCollection.Add raises CollectionChanged itself — CartCollectionView picks
            // up the new row without any ItemsSource reset.
            _cart.Add(new CartLine
            {
                ProductSyncId = product.SyncId,
                Name = product.Name,
                Price = product.CurrentSalePrice ?? 0,
                CostPrice = product.CurrentCostPrice ?? 0,
                Quantity = 1,
                Stock = product.CurrentStock
            });
            RefreshCartTotals();
        }
    }

    private void OnIncrementClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not CartLine line) return;
        line.Quantity++;
        RefreshCartTotals();
    }

    private void OnDecrementClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not CartLine line) return;
        if (line.Quantity > 1)
        {
            line.Quantity--;
        }
        else
        {
            _cart.Remove(line);
        }
        RefreshCartTotals();
    }

    private void OnRemoveLineTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not CartLine line) return;
        _cart.Remove(line);
        RefreshCartTotals();
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        _cart.Clear();
        RefreshCartTotals();
    }

    private void RefreshCartTotals()
    {
        EmptyCartLayout.IsVisible = _cart.Count == 0;

        var totalItems = _cart.Sum(c => c.Quantity);
        var grandTotal = _cart.Sum(c => c.LineTotal);
        CartCountLabel.Text = totalItems == 1 ? "1 item" : $"{totalItems:0.##} items";
        TotalLabel.Text = $"₱{grandTotal:N2}";
        ChargeButton.Text = _cart.Count == 0 ? "Charge" : $"Charge ₱{grandTotal:N2}";
        ChargeButton.IsEnabled = _cart.Count > 0;
    }

    private async void OnChargeClicked(object? sender, EventArgs e)
    {
        if (_cart.Count == 0) return;

        var grandTotal = _cart.Sum(c => c.LineTotal);
        var payment = await new PaymentPage(grandTotal).ShowAsync(Navigation);
        if (payment == null) return; // cashier backed out of the payment screen

        ChargeButton.IsEnabled = false;
        try
        {
            var sale = new LocalSale
            {
                SyncId = Guid.NewGuid(),
                InvoiceDate = DateTime.UtcNow,
                CustomerId = null,
                // Mirrors the web checkout, which also never sets Invoice.LocationId today —
                // it isn't FK-enforced at the database level, so 0 is a safe, consistent default
                // until branch/location selection is actually wired up end to end.
                LocationId = 0,
                Notes = payment.Method == Models.PaymentMethod.Card ? "Paid by card" : "Paid by cash",
                PaidAmount = payment.AmountReceived,
                GrandTotal = grandTotal,
                IsSynced = false,
                CreatedUtc = DateTime.UtcNow
            };

            var items = _cart.Select(c => new LocalSaleItem
            {
                SyncId = Guid.NewGuid(),
                SaleSyncId = sale.SyncId,
                ProductSyncId = c.ProductSyncId,
                ProductName = c.Name,
                Quantity = c.Quantity,
                SalePrice = c.Price,
                CostPrice = c.CostPrice
            }).ToList();

            await _localDatabase.SaveSaleAsync(sale, items);

            _cart.Clear();
            RefreshCartTotals();
            await LoadProductsAsync(); // refresh tiles with the locally decremented stock

            // Best-effort immediate sync — if there's no connection this just leaves the sale
            // queued locally, exactly as if the device had been offline the whole time.
            _ = SyncQuietlyAsync();

            var message = payment.Method == Models.PaymentMethod.Cash && payment.Change > 0
                ? $"Charged ₱{grandTotal:N2} cash. Change due: ₱{payment.Change:N2}."
                : $"Charged ₱{grandTotal:N2} ({payment.Method}).";
            await DisplayAlertAsync("Sale Complete", message, "OK");
        }
        finally
        {
            ChargeButton.IsEnabled = _cart.Count > 0;
        }
    }

    private async Task SyncQuietlyAsync()
    {
        try
        {
            await _syncService.SyncSalesAsync();
        }
        catch
        {
            // Offline or server unreachable — the sale stays queued locally and will sync on the
            // next successful attempt (next Charge, or the periodic sync elsewhere in the app).
        }
        finally
        {
            await RefreshPendingSyncLabelAsync();
        }
    }
}
