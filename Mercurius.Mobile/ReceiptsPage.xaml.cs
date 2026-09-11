using Mercurius.Mobile.Data;
using Mercurius.Mobile.Models;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class ReceiptsPage : ContentPage
{
    private readonly LocalDatabase _localDatabase;
    private readonly SyncService _syncService;
    private List<LocalSale> _sales = new();

    public ReceiptsPage(LocalDatabase localDatabase, SyncService syncService)
    {
        InitializeComponent();
        _localDatabase = localDatabase;
        _syncService = syncService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadReceiptsAsync();
    }

    private async Task LoadReceiptsAsync()
    {
        _sales = await _localDatabase.GetAllSalesAsync();

        var rows = _sales.Select(s => new ReceiptRow
        {
            SyncId = s.SyncId,
            InvoiceDate = s.InvoiceDate,
            GrandTotal = s.GrandTotal,
            IsSynced = s.IsSynced
        }).ToList();

        ReceiptsCollectionView.ItemsSource = rows;
        EmptyListLayout.IsVisible = rows.Count == 0;

        var unsyncedCount = rows.Count(r => !r.IsSynced);
        SummaryLabel.Text = unsyncedCount == 0
            ? $"{rows.Count} sale(s) — all synced"
            : $"{rows.Count} sale(s) — {unsyncedCount} pending sync";
    }

    private async void OnReceiptSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ReceiptRow row)
        {
            DetailLayout.IsVisible = false;
            NoSelectionLayout.IsVisible = true;
            return;
        }

        var sale = _sales.FirstOrDefault(s => s.SyncId == row.SyncId);
        if (sale == null) return;

        var items = await _localDatabase.GetSaleItemsAsync(sale.SyncId);
        DetailItemsCollectionView.ItemsSource = items.Select(i => new ReceiptItemRow
        {
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            SalePrice = i.SalePrice
        }).ToList();

        DetailTotalLabel.Text = row.TotalDisplay;
        DetailDateLabel.Text = row.DateDisplay;
        DetailStatusLabel.Text = row.StatusText;

        var isSynced = row.IsSynced;
        DetailStatusLabel.TextColor = isSynced ? Color.FromArgb("#2E7D32") : Color.FromArgb("#D32F2F");
        DetailStatusBorder.BackgroundColor = isSynced ? Color.FromArgb("#E8F5EA") : Color.FromArgb("#FBE9E9");

        NoSelectionLayout.IsVisible = false;
        DetailLayout.IsVisible = true;
    }

    private async void OnSyncNowClicked(object? sender, EventArgs e)
    {
        SyncBusyIndicator.IsVisible = true;
        SyncBusyIndicator.IsRunning = true;
        try
        {
            await _syncService.SyncSalesAsync();
            await LoadReceiptsAsync();
        }
        catch
        {
            await DisplayAlertAsync("Sync Failed", "Couldn't reach the server. Sales stay queued and will sync automatically once you're back online.", "OK");
        }
        finally
        {
            SyncBusyIndicator.IsVisible = false;
            SyncBusyIndicator.IsRunning = false;
        }
    }
}
