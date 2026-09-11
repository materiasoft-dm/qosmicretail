using Mercurius.Mobile.Data;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class MainPage : ContentPage
{
    private readonly SessionService _sessionService;
    private readonly LocalDatabase _localDatabase;
    private readonly SyncService _syncService;

    public MainPage(SessionService sessionService, LocalDatabase localDatabase, SyncService syncService)
    {
        InitializeComponent();
        _sessionService = sessionService;
        _localDatabase = localDatabase;
        _syncService = syncService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var fullName = await _sessionService.GetFullNameAsync();
        GreetingLabel.Text = string.IsNullOrWhiteSpace(fullName) ? "Welcome" : $"Welcome, {fullName.Split(',').Last().Trim()}";

        PlatformLabel.Text = DeviceInfo.Platform.ToString();
        RefreshConnectionStatus();
        await RefreshStatsAsync();
    }

    private async Task RefreshStatsAsync()
    {
        var products = await _localDatabase.GetProductsAsync();
        ProductCountLabel.Text = products.Count.ToString("N0");
        LowStockCountLabel.Text = products.Count(p => p.IsLowStock).ToString("N0");

        var lastSynced = await _localDatabase.GetLastSyncedAsync("products");
        LastSyncedLabel.Text = lastSynced.HasValue ? lastSynced.Value.ToLocalTime().ToString("MMM d, h:mm tt") : "Never";
    }

    private void RefreshConnectionStatus()
    {
        var online = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        ConnectionLabel.Text = online ? "Online" : "Offline";
        ConnectionDot.Fill = online ? Color.FromArgb("#4CAF50") : Color.FromArgb("#D32F2F");
    }

    private async void OnGoToProductsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ProductsPage");
    }

    private async void OnSyncNowClicked(object? sender, EventArgs e)
    {
        SyncNowButton.IsEnabled = false;
        SyncBusyIndicator.IsVisible = true;
        SyncBusyIndicator.IsRunning = true;
        SyncStatusLabel.IsVisible = true;
        SyncStatusLabel.Text = "Syncing…";

        try
        {
            var changedCount = await _syncService.SyncProductsAsync();
            SyncStatusLabel.Text = changedCount > 0
                ? $"Synced {changedCount} update(s) • {DateTime.Now:h:mm tt}"
                : $"Up to date • {DateTime.Now:h:mm tt}";
            await RefreshStatsAsync();
        }
        catch (Exception ex)
        {
            SyncStatusLabel.Text = "Sync failed — showing cached data";
            System.Diagnostics.Debug.WriteLine($"Sync failed: {ex.Message}");
        }
        finally
        {
            RefreshConnectionStatus();
            SyncNowButton.IsEnabled = true;
            SyncBusyIndicator.IsVisible = false;
            SyncBusyIndicator.IsRunning = false;
        }
    }
}
