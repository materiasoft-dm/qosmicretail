using Mercurius.Mobile.Data;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class ProductsPage : ContentPage
{
    private readonly LocalDatabase _localDatabase;
    private readonly SyncService _syncService;
    private System.Timers.Timer? _searchDebounceTimer;

    public ProductsPage(LocalDatabase localDatabase, SyncService syncService)
    {
        InitializeComponent();
        _localDatabase = localDatabase;
        _syncService = syncService;
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
            var changedCount = await _syncService.SyncProductsAsync();
            SyncStatusLabel.Text = changedCount > 0
                ? $"Synced {changedCount} update(s) • {DateTime.Now:h:mm tt}"
                : $"Up to date • {DateTime.Now:h:mm tt}";
            await LoadFromLocalAsync(SearchEntry.Text);
        }
        catch (Exception ex)
        {
            SyncStatusLabel.Text = "Offline — showing cached products";
            System.Diagnostics.Debug.WriteLine($"Sync failed: {ex.Message}");
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
