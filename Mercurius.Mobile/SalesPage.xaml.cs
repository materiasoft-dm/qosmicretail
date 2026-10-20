using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Views;
using Mercurius.Mobile.Data;
using Mercurius.Mobile.Models;
using Mercurius.Mobile.Services;
using Mercurius.Shared.Bir;

namespace Mercurius.Mobile;

public partial class SalesPage : ContentPage
{
    private const string AllCategory = "All";

    private readonly LocalDatabase _localDatabase;
    private readonly SyncService _syncService;
    private readonly Services.DeviceSettingsService _deviceSettingsService;

    private List<LocalProduct> _allProducts = new();
    // Backing list for the category picker popup (includes the synthetic "All" entry at index 0).
    private List<string> _categoryOptions = new() { AllCategory };
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

    private readonly HardwareScannerService _hardwareScanner;
    private bool _isPageActive;
    private int _openPopups;

    private readonly SyncCoordinator _syncCoordinator;
    private readonly SessionService _sessionService;
    private readonly BirService _birService;

    // BIR: this device's terminal registration (seller TIN, VAT status), the tenant's discount
    // types, and the discount/buyer details the cashier added to the current ticket.
    private LocalTerminal? _terminal;
    private IReadOnlyList<DiscountRule> _discountRules = Array.Empty<DiscountRule>();
    private InvoiceDetails _invoiceDetails = new();

    public SalesPage(LocalDatabase localDatabase, SyncService syncService, Services.DeviceSettingsService deviceSettingsService,
        HardwareScannerService hardwareScanner, SyncCoordinator syncCoordinator, SessionService sessionService, BirService birService)
    {
        InitializeComponent();
        _localDatabase = localDatabase;
        _syncService = syncService;
        _deviceSettingsService = deviceSettingsService;
        _hardwareScanner = hardwareScanner;
        _syncCoordinator = syncCoordinator;
        _sessionService = sessionService;
        _birService = birService;
        CartCollectionView.ItemsSource = _cart;
        CameraScanButton.IsVisible = BarcodeScanner.IsCameraScanAvailable;
        SearchEntry.Unfocused += OnSearchEntryUnfocused;
        ApplyViewMode();
    }

    // ---------------------------------------------------------------- always-ready scanning
    //
    // A hardware scanner must work the moment this screen is showing, without tapping anything:
    //   Android — MainActivity routes unfocused keystrokes to HardwareScannerService, which raises
    //             Scanned (so no on-screen keyboard pops up either).
    //   Windows — no such global hook; instead the search box keeps focus whenever no popup is
    //             open, and the scan arrives through its Completed event.

    private void StartListeningForScans()
    {
        _isPageActive = true;
        _hardwareScanner.Scanned -= OnHardwareScanned;
        _hardwareScanner.Scanned += OnHardwareScanned;
        _hardwareScanner.IsListening = true;
        ScannerReadyChip.IsVisible = _hardwareScanner.IsScannerLikelyConnected();
        KeepSearchFocusForScanner();
    }

    private void StopListeningForScans()
    {
        _isPageActive = false;
        _hardwareScanner.IsListening = false;
        _hardwareScanner.Scanned -= OnHardwareScanned;
    }

    private async void OnHardwareScanned(string code)
    {
        if (_openPopups > 0) return; // e.g. mid-payment — don't add to the ticket underneath it
        ScannerReadyChip.IsVisible = true; // a scan just proved a scanner is attached
        await HandleScannedOrTypedCodeAsync(code);
    }

    private void KeepSearchFocusForScanner()
    {
        if (DeviceInfo.Platform != DevicePlatform.WinUI) return;
        if (!_isPageActive || _openPopups > 0) return;
        SearchEntry.Focus();
    }

    private void OnSearchEntryUnfocused(object? sender, FocusEventArgs e)
    {
        if (DeviceInfo.Platform != DevicePlatform.WinUI) return;
        // Deferred: a tap that moved focus away may be about to open a popup (category, quantity,
        // payment) — let it register first, then only take focus back if nothing else claimed it.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), KeepSearchFocusForScanner);
    }

    // Every popup on this page goes through here, so scans and focus-keeping pause while one is open.
    private async Task<object?> ShowTrackedPopupAsync(CommunityToolkit.Maui.Views.Popup popup)
    {
        _openPopups++;
        try
        {
            return await this.ShowPopupAsync(popup);
        }
        finally
        {
            _openPopups--;
            KeepSearchFocusForScanner();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopListeningForScans();
        _syncCoordinator.SyncCompleted -= OnSyncCompleted;
    }

    // Mirrors Resources/Styles/Colors.xaml's MidnightBlue/Gray400 rather than looking them up via
    // StaticResource at runtime — this is the segmented switch's "active"/"inactive" text color,
    // and both segments' colors are only ever set from code (the active one depends on whichever
    // mode Preferences last persisted, so there's no single correct default to bake into XAML).
    private static readonly Color ActiveSegmentColor = Colors.White;
    private static readonly Color ActiveSegmentTextColor = Color.FromArgb("#1A1D2E");
    private static readonly Color InactiveSegmentTextColor = Color.FromArgb("#919191");

    private void ApplyViewMode()
    {
        ProductsGridView.IsVisible = _isGridView;
        ProductsListView.IsVisible = !_isGridView;

        TileToggleBorder.BackgroundColor = _isGridView ? ActiveSegmentColor : Colors.Transparent;
        TileToggleLabel.TextColor = _isGridView ? ActiveSegmentTextColor : InactiveSegmentTextColor;
        ListToggleBorder.BackgroundColor = !_isGridView ? ActiveSegmentColor : Colors.Transparent;
        ListToggleLabel.TextColor = !_isGridView ? ActiveSegmentTextColor : InactiveSegmentTextColor;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartListeningForScans();
        _syncCoordinator.SyncCompleted -= OnSyncCompleted;
        _syncCoordinator.SyncCompleted += OnSyncCompleted;
        await LoadBirSettingsAsync();
        await LoadProductsAsync();
        await RefreshPendingSyncLabelAsync();
    }

    private async Task LoadBirSettingsAsync()
    {
        _terminal = await _birService.GetTerminalAsync();
        _discountRules = await _birService.GetDiscountRulesAsync();
        RefreshCartTotals();
    }

    private async Task LoadProductsAsync()
    {
        _allProducts = await _localDatabase.GetProductsAsync();

        _categoryOptions = new List<string> { AllCategory };
        _categoryOptions.AddRange(_allProducts
            .Select(p => p.CategoryName)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)!);

        // A category that existed on the previous load but no longer does (e.g. its last product
        // was removed by a sync) can't stay selected — fall back to "All" rather than filtering
        // to a category the dropdown can no longer offer.
        if (!_categoryOptions.Contains(_selectedCategory))
        {
            _selectedCategory = AllCategory;
        }
        CategoryDropdownLabel.Text = _selectedCategory;

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
        // The server refused them (rather than being unreachable), e.g. this account can't sell:
        // waiting won't fix it, so say why.
        var refusal = pending > 0 ? _syncCoordinator.LastReport?.SaleRejections.FirstOrDefault() : null;
        if (refusal != null) PendingSyncLabel.Text += $" · {refusal}";
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
                p.ProductCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.Barcode?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var results = filtered.OrderBy(p => p.Name).ToList();
        ProductsGridView.ItemsSource = results;
        ProductsListView.ItemsSource = results;
        EmptyStateLayout.IsVisible = results.Count == 0;
    }

    private async void OnCategoryDropdownTapped(object? sender, TappedEventArgs e)
    {
        var result = await ShowTrackedPopupAsync(new CategoryPickerPopup(_categoryOptions));
        if (result is string category)
        {
            _selectedCategory = category;
            CategoryDropdownLabel.Text = category;
            ApplyFilter();
        }
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

    private void OnTileViewSelected(object? sender, TappedEventArgs e) => SetViewMode(isGridView: true);

    private void OnListViewSelected(object? sender, TappedEventArgs e) => SetViewMode(isGridView: false);

    private void SetViewMode(bool isGridView)
    {
        if (_isGridView == isGridView) return; // already the active segment — nothing to do
        _isGridView = isGridView;
        Preferences.Default.Set(ViewModePreferenceKey, _isGridView);
        ApplyViewMode();
    }

    // Enter in the search box — which is exactly what a hardware barcode scanner sends after the
    // code. An exact barcode (or SKU) match goes straight onto the ticket and the box is cleared,
    // ready for the next scan. Anything else is left as an ordinary search.
    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        await HandleScannedOrTypedCodeAsync(SearchEntry.Text?.Trim());
    }

    // Camera scan feeds the exact same path as a hardware scanner / Enter key.
    private async void OnCameraScanClicked(object? sender, EventArgs e)
    {
        string? scanned;
        _openPopups++;
        try { scanned = await BarcodeScanner.ScanAsync(); }
        finally { _openPopups--; }
        if (string.IsNullOrWhiteSpace(scanned)) return;
        SearchEntry.Text = scanned;
        await HandleScannedOrTypedCodeAsync(scanned);
    }

    private async Task HandleScannedOrTypedCodeAsync(string? term)
    {
        if (string.IsNullOrEmpty(term)) return;

        var match =
            _allProducts.FirstOrDefault(p => string.Equals(p.Barcode?.Trim(), term, StringComparison.OrdinalIgnoreCase)) ??
            _allProducts.FirstOrDefault(p => string.Equals(p.ProductCode, term, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            AddToCart(match);
            ResetSearchForNextScan();
            return;
        }

        // Only offer "create it" for something that looks like a scanned code (one token, no
        // spaces) — a typed product name that simply didn't match shouldn't trigger a prompt.
        if (term.Contains(' ')) return;

        bool create;
        _openPopups++;
        try
        {
            create = await DisplayAlertAsync(
                "Barcode not found",
                $"No product has barcode {term}. Add it as a new product?",
                "Add Product", "Cancel");
        }
        finally { _openPopups--; }
        if (!create)
        {
            ResetSearchForNextScan();
            return;
        }

        var all = await _localDatabase.GetProductsAsync();
        var result = await ShowTrackedPopupAsync(new AddProductPopup(all, prefilledBarcode: term));
        if (result is LocalProduct product)
        {
            await _localDatabase.SaveNewProductAsync(product);
            await LoadProductsAsync();
            AddToCart(product);
            _ = PushNewProductQuietlyAsync();
        }
        ResetSearchForNextScan();
    }

    private void ResetSearchForNextScan()
    {
        SearchEntry.Text = string.Empty;
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            // Windows: the search box is where the next scan lands.
            KeepSearchFocusForScanner();
        }
        else
        {
            // Android/iOS: drop focus (and the on-screen keyboard, if a code was typed by hand) —
            // with no text field focused, MainActivity hands the next hardware scan straight to
            // HardwareScannerService.
            SearchEntry.Unfocus();
        }
    }

    private async Task PushNewProductQuietlyAsync()
    {
        try { await _syncCoordinator.SyncNowAsync(); }
        catch { /* offline — stays queued (IsPendingSync) and goes up with the next sync */ }
    }

    private void OnProductTileTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not LocalProduct product) return;
        AddToCart(product);
    }

    private void AddToCart(LocalProduct product)
    {
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
                Stock = product.CurrentStock,
                VatType = product.VatType
            });
            RefreshCartTotals();
        }
    }

    private async void OnCartLineTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not CartLine line) return;

        var result = await ShowTrackedPopupAsync(new QuantityPopup(line));
        if (result is QuantityPopupResult r)
        {
            if (r.Remove)
            {
                _cart.Remove(line);
            }
            else if (r.Quantity > 0)
            {
                line.Quantity = r.Quantity;
            }
            RefreshCartTotals();
        }
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
        _invoiceDetails = new InvoiceDetails();
        RefreshCartTotals();
    }

    private async void OnInvoiceDetailsClicked(object? sender, EventArgs e)
    {
        var result = await ShowTrackedPopupAsync(new InvoiceDetailsPopup(_invoiceDetails, _discountRules));
        if (result is InvoiceDetails details)
        {
            _invoiceDetails = details;
            RefreshCartTotals();
        }
    }

    private TaxSummary ComputeTicket() => BirService.Compute(_cart, _terminal, _invoiceDetails.Discount);

    private void RefreshCartTotals()
    {
        EmptyCartLayout.IsVisible = _cart.Count == 0;

        var summary = ComputeTicket();
        var totalItems = _cart.Sum(c => c.Quantity);
        CartCountLabel.Text = totalItems == 1 ? "1 item" : $"{totalItems:0.##} items";

        var discount = _invoiceDetails.Discount;
        LessVatRow.IsVisible = summary.LessVat != 0;
        LessVatAmountLabel.Text = $"-₱{summary.LessVat:N2}";
        DiscountRow.IsVisible = discount != null;
        DiscountRowLabel.Text = discount == null ? "" : $"{discount.Name} ({discount.ValueText})";
        DiscountAmountLabel.Text = $"-₱{summary.Discount:N2}";
        InvoiceDetailsButton.Text = _invoiceDetails.IsEmpty
            ? "+ Discount or customer"
            : discount != null
                ? $"Discount: {discount.Code}{(_invoiceDetails.HasBuyer ? " · customer added" : "")}"
                : "Customer added";

        TotalLabel.Text = $"₱{summary.AmountDue:N2}";
        ChargeButton.Text = _cart.Count == 0 ? "Charge" : $"Charge ₱{summary.AmountDue:N2}";
        ChargeButton.IsEnabled = _cart.Count > 0;
    }

    private async void OnChargeClicked(object? sender, EventArgs e)
    {
        if (_cart.Count == 0) return;

        var summary = ComputeTicket();
        var grandTotal = summary.AmountDue;
        var details = _invoiceDetails;
        var result = await ShowTrackedPopupAsync(new PaymentPage(grandTotal));
        if (result is not Models.PaymentResult payment) return; // cashier backed out of the payment screen

        ChargeButton.IsEnabled = false;
        try
        {
            var sale = new LocalSale
            {
                SyncId = Guid.NewGuid(),
                InvoiceDate = DateTime.UtcNow,
                CustomerId = null,
                // The clinic this device is assigned to (Settings page) — 0 if never configured,
                // which SyncController.PushInvoices treats the same as before this setting
                // existed (falls back to resolving the signed-in user's own current branch).
                LocationId = _deviceSettingsService.GetSelectedLocationId(),
                Notes = payment.Method == Models.PaymentMethod.Card ? "Paid by card" : "Paid by cash",
                PaymentMethod = payment.Method.ToString(),
                PaidAmount = payment.AmountReceived,
                GrandTotal = grandTotal,
                IsSynced = false,
                CreatedUtc = DateTime.UtcNow,
                // Provenance, so the Receipts list can show this sale properly while it's still
                // waiting to upload (the server's copy replaces these once it comes back).
                IsFromThisDevice = true,
                LocationName = _deviceSettingsService.GetSelectedLocationName(),
                CashierUserId = await _sessionService.GetUserIdAsync(),
                CashierName = await _sessionService.GetFullNameAsync(),
                SourceChannel = "MobileApp",
                SourceDeviceId = _deviceSettingsService.GetDeviceId(),
                SourceDeviceName = _deviceSettingsService.GetTerminalName(),
                // BIR: the invoice's tax summary and whatever the cashier added to the ticket.
                GrossAmount = summary.Gross,
                VatableSales = summary.VatableSales,
                VatAmount = summary.VatAmount,
                VatExemptSales = summary.VatExemptSales,
                ZeroRatedSales = summary.ZeroRatedSales,
                LessVat = summary.LessVat,
                DiscountAmount = summary.Discount,
                DiscountTypeId = details.Discount?.Id,
                DiscountName = details.Discount?.ReceiptLabel,
                SpecialDiscountType = (int)(details.Discount?.Category ?? SpecialDiscountType.None),
                SpecialDiscountIdNumber = details.DiscountIdNumber,
                SpecialDiscountHolderName = details.DiscountHolderName,
                SpecialDiscountHolderTin = details.DiscountHolderTin,
                BuyerName = details.BuyerName,
                BuyerTin = details.BuyerTin,
                BuyerAddress = details.BuyerAddress,
                BuyerBusinessStyle = details.BuyerBusinessStyle
            };

            var items = _cart.Select((c, i) =>
            {
                var tax = summary.Lines[i];
                return new LocalSaleItem
                {
                    SyncId = Guid.NewGuid(),
                    SaleSyncId = sale.SyncId,
                    ProductSyncId = c.ProductSyncId,
                    ProductName = c.Name,
                    Quantity = c.Quantity,
                    SalePrice = c.Price,
                    CostPrice = c.CostPrice,
                    VatType = (int)tax.VatType,
                    VatAmount = tax.VatAmount,
                    DiscountAmount = tax.Discount,
                    // Only when it differs from Quantity * SalePrice (see LocalSaleItem.EffectiveLineTotal).
                    LineTotal = tax.Net != tax.Gross ? tax.Net : 0
                };
            }).ToList();

            // Numbers the invoice against this terminal and prints it, in one transaction.
            await _localDatabase.SaveSaleAsync(sale, items, (s, t) => _birService.RenderReceipt(s, items, t, details.Discount));

            _cart.Clear();
            _invoiceDetails = new InvoiceDetails();
            RefreshCartTotals();
            await LoadProductsAsync(); // refresh tiles with the locally decremented stock

            // Best-effort immediate sync — if there's no connection this just leaves the sale
            // queued locally, exactly as if the device had been offline the whole time.
            _ = SyncQuietlyAsync();

            var message = payment.Method == Models.PaymentMethod.Cash && payment.Change > 0
                ? $"Charged ₱{grandTotal:N2} cash. Change due: ₱{payment.Change:N2}."
                : $"Charged ₱{grandTotal:N2} ({payment.Method}).";
            await ShowTrackedPopupAsync(new ReceiptPopup("Sale complete", message, sale.ReceiptText ?? ""));
        }
        finally
        {
            ChargeButton.IsEnabled = _cart.Count > 0;
        }
    }

    private async Task SyncQuietlyAsync()
    {
        // Through the coordinator, so this can't race the periodic/background sync into pushing
        // the same queued sale twice at once. Never throws — failures leave the sale queued.
        await _syncCoordinator.SyncNowAsync();
        await RefreshPendingSyncLabelAsync();
    }

    private async void OnSyncCompleted(SyncReport report)
    {
        // Background syncs (timer, reconnect) can upload sales while this page is open.
        await RefreshPendingSyncLabelAsync();
        // The terminal's registration (TIN, MIN, VAT status) and the discount types may have changed.
        await LoadBirSettingsAsync();
        if (report.ProductsPulled > 0) await LoadProductsAsync();
    }
}
