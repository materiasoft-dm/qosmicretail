using CommunityToolkit.Maui.Views;
using Mercurius.Mobile.Data;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class ProductsPage : ContentPage
{
    private readonly LocalDatabase _localDatabase;
    private readonly SyncCoordinator _syncCoordinator;
    private System.Timers.Timer? _searchDebounceTimer;

    public ProductsPage(LocalDatabase localDatabase, SyncCoordinator syncCoordinator)
    {
        InitializeComponent();
        _localDatabase = localDatabase;
        _syncCoordinator = syncCoordinator;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Show whatever's already cached locally immediately — the app is usable offline even
        // before any sync completes. Then quietly sync in the background and refresh the list.
        await LoadFromLocalAsync();
        _ = SyncInBackgroundAsync();
    }

    private async Task LoadFromLocalAsync(string? searchText = null)
    {
        var products = await _localDatabase.GetProductsAsync(searchText);
        ProductsCollectionView.ItemsSource = products;
        EmptyStateLayout.IsVisible = products.Count == 0;
        ProductCountBadge.Text = products.Count == 1 ? "1 item" : $"{products.Count:N0} items";
    }

    private async Task SyncInBackgroundAsync()
    {
        SetSyncBusy(true);
        SyncStatusLabel.Text = "Syncing…";
        try
        {
            // Through the coordinator: a product pushed from here must not race the background
            // pass pushing the same one (two concurrent creates of one SyncId — one would fail).
            var report = await _syncCoordinator.SyncNowAsync();
            var changedCount = report.ProductsPulled;
            SyncStatusLabel.Text = report.Offline || report.Errors.Count > 0
                ? "Offline — showing cached products"
                : changedCount > 0
                    ? $"Synced {changedCount} update(s) • {DateTime.Now:h:mm tt}"
                    : $"Up to date • {DateTime.Now:h:mm tt}";
            await LoadFromLocalAsync(SearchEntry.Text);
        }
        finally
        {
            SetSyncBusy(false);
        }
    }

    private void SetSyncBusy(bool busy)
    {
        SyncButton.IsEnabled = !busy;
        SyncBusyIndicator.IsVisible = busy;
        SyncBusyIndicator.IsRunning = busy;
    }

    private async void OnSyncButtonClicked(object? sender, EventArgs e)
    {
        await SyncInBackgroundAsync();
    }

    private async void OnAddProductClicked(object? sender, EventArgs e)
    {
        var existing = await _localDatabase.GetProductsAsync();
        var result = await this.ShowPopupAsync(new AddProductPopup(existing));
        if (result is not LocalProduct product) return; // cancelled

        // Saved locally first so it's usable immediately (and offline); the sync below then pushes
        // it, and if that fails it simply stays queued (IsPendingSync) for the next sync.
        await _localDatabase.SaveNewProductAsync(product);
        await LoadFromLocalAsync(SearchEntry.Text);
        await SyncInBackgroundAsync();
    }

    private async void OnProductRowTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not LocalProduct product) return;

        var existing = await _localDatabase.GetProductsAsync();
        var result = await this.ShowPopupAsync(new AddProductPopup(existing, editing: product));
        if (result is not LocalProduct edited) return; // cancelled

        // Same local-first flow as adding: visible immediately, pushed now or on the next sync.
        await _localDatabase.SaveEditedProductAsync(edited);
        await LoadFromLocalAsync(SearchEntry.Text);
        await SyncInBackgroundAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await SyncInBackgroundAsync();
        ProductsRefreshView.IsRefreshing = false;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        // Debounce so we're not re-querying the local DB on every keystroke.
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer = new System.Timers.Timer(250) { AutoReset = false };
        _searchDebounceTimer.Elapsed += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(async () => await LoadFromLocalAsync(e.NewTextValue));
        };
        _searchDebounceTimer.Start();
    }
}
