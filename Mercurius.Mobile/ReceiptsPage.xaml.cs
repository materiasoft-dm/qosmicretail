using Mercurius.Mobile.Data;
using Mercurius.Mobile.Models;
using CommunityToolkit.Maui.Views;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

// Every sale this device knows about: its own (including ones still waiting to upload) plus
// every other terminal's and the web's, pulled by SyncService.PullReceiptsAsync (last 30 days).
// All filtering happens locally, so the page works — and stays fast — with no connection.
public partial class ReceiptsPage : ContentPage
{
    private const string ScopePreferenceKey = "Receipts_Scope";
    private const string PeriodPreferenceKey = "Receipts_PeriodDays";

    private enum Scope { Device, Branch, All }

    private readonly LocalDatabase _localDatabase;
    private readonly SyncCoordinator _syncCoordinator;
    private readonly DeviceSettingsService _deviceSettings;
    private readonly BirService _birService;
    private LocalSale? _shownSale;

    private List<LocalSale> _sales = new();
    private Dictionary<Guid, List<LocalSaleItem>> _itemsBySale = new();
    private Scope _scope;
    private int _periodDays;
    private System.Timers.Timer? _searchDebounce;

    private static readonly Color ActiveSegment = Colors.White;
    private static readonly Color ActiveSegmentText = Color.FromArgb("#1A1D2E");
    private static readonly Color InactiveSegmentText = Color.FromArgb("#919191");

    public ReceiptsPage(LocalDatabase localDatabase, SyncCoordinator syncCoordinator, DeviceSettingsService deviceSettings, BirService birService)
    {
        InitializeComponent();
        _birService = birService;
        _localDatabase = localDatabase;
        _syncCoordinator = syncCoordinator;
        _deviceSettings = deviceSettings;

        // Default to the branch view once the device knows its branch — that's what a cashier
        // usually means by "today's sales"; before that, only "this device" is meaningful.
        var defaultScope = _deviceSettings.GetSelectedLocationId() > 0 ? Scope.Branch : Scope.Device;
        _scope = Enum.TryParse<Scope>(Preferences.Default.Get(ScopePreferenceKey, defaultScope.ToString()), out var s) ? s : defaultScope;
        _periodDays = Preferences.Default.Get(PeriodPreferenceKey, 1);
        ApplySegmentStyles();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _syncCoordinator.SyncCompleted -= OnSyncCompleted;
        _syncCoordinator.SyncCompleted += OnSyncCompleted;

        await LoadReceiptsAsync();       // show what's cached immediately...
        _ = _syncCoordinator.SyncNowAsync(); // ...then catch up in the background
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _syncCoordinator.SyncCompleted -= OnSyncCompleted;
    }

    private async void OnSyncCompleted(SyncReport report) => await LoadReceiptsAsync(keepSelection: true);

    // ---------------------------------------------------------------- data + filtering

    private async Task LoadReceiptsAsync(bool keepSelection = false)
    {
        _sales = await _localDatabase.GetAllSalesAsync();
        var allItems = await _localDatabase.GetAllSaleItemsAsync();
        _itemsBySale = allItems.GroupBy(i => i.SaleSyncId).ToDictionary(g => g.Key, g => g.ToList());
        ApplyFilter(keepSelection);
    }

    private void ApplyFilter(bool keepSelection = false)
    {
        var myDeviceId = _deviceSettings.GetDeviceId();
        var myBranchId = _deviceSettings.GetSelectedLocationId();
        var since = _periodDays <= 1
            ? DateTime.Today.ToUniversalTime()                     // "Today" = since local midnight
            : DateTime.UtcNow.AddDays(-_periodDays);

        bool IsMine(LocalSale s) => s.IsFromThisDevice || (s.SourceDeviceId != null && s.SourceDeviceId == myDeviceId);

        IEnumerable<LocalSale> filtered = _sales.Where(s => s.InvoiceDate >= since || !s.IsSynced); // never hide a pending sale
        filtered = _scope switch
        {
            Scope.Device => filtered.Where(IsMine),
            Scope.Branch when myBranchId > 0 => filtered.Where(s => s.LocationId == myBranchId || (IsMine(s) && s.LocationId == 0)),
            _ => filtered
        };

        var term = ReceiptSearchEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            filtered = filtered.Where(s =>
                Contains(s.InvoiceNumber, term) || Contains(s.CashierName, term) || Contains(s.SourceDeviceName, term) ||
                Contains(s.LocationName, term) ||
                (_itemsBySale.TryGetValue(s.SyncId, out var items) && items.Any(i => Contains(i.ProductName, term))));
        }

        var rows = filtered
            .OrderByDescending(s => s.InvoiceDate)
            .Select(s => new ReceiptRow(s, IsMine(s)))
            .ToList();

        var previouslySelected = keepSelection ? (ReceiptsCollectionView.SelectedItem as ReceiptRow)?.SyncId : null;
        ReceiptsCollectionView.ItemsSource = rows;
        EmptyListLayout.IsVisible = rows.Count == 0;
        EmptyListHintLabel.Text = _scope == Scope.Branch && myBranchId == 0
            ? "Pick this device's clinic in Settings to see its branch's sales"
            : "Try a wider date range or scope";

        if (previouslySelected != null)
        {
            var again = rows.FirstOrDefault(r => r.SyncId == previouslySelected);
            if (again != null)
            {
                ReceiptsCollectionView.SelectedItem = again;
                ShowDetail(again); // refresh in place (e.g. a refund just synced in)
            }
        }

        UpdateSummary(rows);
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;

    private void UpdateSummary(List<ReceiptRow> rows)
    {
        // Voided sales don't count toward the total; refunds are netted out.
        var counted = rows.Where(r => !r.IsDeleted).ToList();
        var net = counted.Sum(r => r.Sale.GrandTotal - r.Sale.RefundedAmount);
        var pending = _sales.Count(s => !s.IsSynced);

        var lastSync = _syncCoordinator.LastSuccessfulSyncUtc;
        var syncText = _syncCoordinator.LastReport?.Offline == true ? "offline"
            : lastSync.HasValue ? $"synced {lastSync.Value.ToLocalTime():h:mm tt}"
            : "not synced yet";

        SummaryLabel.Text = $"{counted.Count} sale{(counted.Count == 1 ? "" : "s")} · ₱{net:N2}" +
                            (pending > 0 ? $" · {pending} pending upload" : "") +
                            $" · {syncText}";
    }

    // ---------------------------------------------------------------- filter controls

    private void OnScopeTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string p && Enum.TryParse<Scope>(p, out var scope))
        {
            _scope = scope;
            Preferences.Default.Set(ScopePreferenceKey, scope.ToString());
            ApplySegmentStyles();
            ApplyFilter();
        }
    }

    private void OnPeriodTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string p && int.TryParse(p, out var days))
        {
            _periodDays = days;
            Preferences.Default.Set(PeriodPreferenceKey, days);
            ApplySegmentStyles();
            ApplyFilter();
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounce?.Stop();
        _searchDebounce = new System.Timers.Timer(250) { AutoReset = false };
        _searchDebounce.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(() => ApplyFilter());
        _searchDebounce.Start();
    }

    private void ApplySegmentStyles()
    {
        void Style(Border segment, Label label, bool active)
        {
            segment.BackgroundColor = active ? ActiveSegment : Colors.Transparent;
            label.TextColor = active ? ActiveSegmentText : InactiveSegmentText;
        }

        Style(ScopeDeviceSegment, ScopeDeviceLabel, _scope == Scope.Device);
        Style(ScopeBranchSegment, ScopeBranchLabel, _scope == Scope.Branch);
        Style(ScopeAllSegment, ScopeAllLabel, _scope == Scope.All);
        Style(PeriodTodaySegment, PeriodTodayLabel, _periodDays <= 1);
        Style(PeriodWeekSegment, PeriodWeekLabel, _periodDays == 7);
        Style(PeriodMonthSegment, PeriodMonthLabel, _periodDays >= 30);
    }

    // ---------------------------------------------------------------- detail

    private void OnReceiptSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ReceiptRow row)
        {
            DetailLayout.IsVisible = false;
            NoSelectionLayout.IsVisible = true;
            return;
        }
        ShowDetail(row);
    }

    private void ShowDetail(ReceiptRow row)
    {
        var sale = row.Sale;
        DetailTotalLabel.Text = row.TotalDisplay;
        DetailStatusLabel.Text = row.StatusText;
        DetailStatusLabel.TextColor = row.StatusTextColor;
        DetailStatusBorder.BackgroundColor = row.StatusBackgroundColor;

        DetailNumberLabel.Text = row.NumberDisplay;
        DetailDateLabel.Text = sale.InvoiceDate.ToLocalTime().ToString("ddd, MMM d yyyy · h:mm tt");
        DetailSourceLabel.Text = row.SourceDisplay;
        DetailBranchLabel.Text = string.IsNullOrWhiteSpace(sale.LocationName) ? "—" : sale.LocationName;
        DetailPaymentLabel.Text = string.IsNullOrWhiteSpace(sale.Notes)
            ? $"Paid ₱{sale.PaidAmount:N2}"
            : $"{sale.Notes} · ₱{sale.PaidAmount:N2} received";

        var hasRefund = sale.HasRefund || sale.RefundedAmount > 0;
        DetailRefundCaption.IsVisible = hasRefund;
        DetailRefundLabel.IsVisible = hasRefund;
        DetailRefundLabel.Text = $"₱{sale.RefundedAmount:N2}";

        DetailItemsStack.Children.Clear();
        var items = _itemsBySale.TryGetValue(sale.SyncId, out var list) ? list : new List<LocalSaleItem>();
        foreach (var item in items.Select(i => new ReceiptItemRow
                 {
                     ProductName = i.ProductName,
                     Quantity = i.Quantity,
                     SalePrice = i.SalePrice,
                     LineTotal = i.EffectiveLineTotal,
                     RefundedQuantity = i.RefundedQuantity
                 }))
        {
            DetailItemsStack.Children.Add(BuildItemRow(item));
        }

        // Only this terminal's own invoices carry their printed text.
        _shownSale = sale;
        ReprintButton.IsVisible = !string.IsNullOrEmpty(sale.ReceiptText);

        NoSelectionLayout.IsVisible = false;
        DetailLayout.IsVisible = true;
    }

    private async void OnReprintClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_shownSale?.ReceiptText)) return;
        await this.ShowPopupAsync(new ReceiptPopup("Reprint", _shownSale.InvoiceNumber, _birService.RenderReprint(_shownSale)));
    }

    private static View BuildItemRow(ReceiptItemRow item)
    {
        var info = new VerticalStackLayout
        {
            Children =
            {
                new Label { Text = item.ProductName, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1A1D2E") },
                new Label { Text = item.QuantityDisplay, FontSize = 11, TextColor = Color.FromArgb("#919191") }
            }
        };
        if (item.HasRefund)
        {
            info.Children.Add(new Label { Text = item.RefundDisplay, FontSize = 11, TextColor = Color.FromArgb("#E65100") });
        }

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Padding = new Thickness(0, 6)
        };
        grid.Add(info, 0, 0);
        grid.Add(new Label
        {
            Text = item.LineTotalDisplay,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1A1D2E"),
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);
        return grid;
    }

    // ---------------------------------------------------------------- sync

    private async void OnSyncNowClicked(object? sender, EventArgs e) => await RunManualSyncAsync();

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await RunManualSyncAsync();
        ReceiptsRefreshView.IsRefreshing = false;
    }

    private async Task RunManualSyncAsync()
    {
        SyncBusyIndicator.IsVisible = true;
        SyncBusyIndicator.IsRunning = true;
        try
        {
            var report = await _syncCoordinator.SyncNowAsync();
            await LoadReceiptsAsync(keepSelection: true);
            if (report.Offline)
            {
                await DisplayAlertAsync("You're offline", "Showing what's saved on this device. Sales made here stay queued and upload automatically once you're back online.", "OK");
            }
            else if (report.Errors.Count > 0)
            {
                await DisplayAlertAsync("Sync incomplete",
                    "Some data couldn't be synced right now — nothing is lost, it will retry automatically.\n\n" + string.Join("\n", report.Errors), "OK");
            }
        }
        finally
        {
            SyncBusyIndicator.IsVisible = false;
            SyncBusyIndicator.IsRunning = false;
        }
    }
}
