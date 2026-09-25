using System.Globalization;
using CommunityToolkit.Maui.Views;
using Mercurius.Mobile.Data;

namespace Mercurius.Mobile;

// Form for creating a product on the device, or editing an existing one (pass `editing`).
// Validates locally (required fields, non-negative numbers, SKU/barcode not already used by
// another product) and returns a ready-to-save LocalProduct via Close(...), or null if cancelled.
// Saving/syncing is the caller's job — this popup only collects and validates input.
public partial class AddProductPopup : Popup
{
    private const int MaxSuggestions = 5;

    private readonly List<string> _categories;
    private readonly HashSet<string> _existingCodes;
    private readonly Dictionary<string, string> _existingBarcodes; // barcode -> product name
    private readonly LocalProduct? _editing;
    private bool _suppressCategorySuggestions;

    // prefilledBarcode: set when the Sell page scanned a barcode no product has yet, so the
    // cashier can create that product on the spot without scanning it a second time.
    // editing: the product to edit; null to create a new one.
    public AddProductPopup(IReadOnlyCollection<LocalProduct> existingProducts, string? prefilledBarcode = null, LocalProduct? editing = null)
    {
        InitializeComponent();
        _editing = editing;

        // The product being edited must not collide with its own SKU/barcode.
        var others = existingProducts.Where(p => editing == null || p.SyncId != editing.SyncId).ToList();

        _categories = existingProducts
            .Select(p => p.CategoryName)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
        _existingCodes = new HashSet<string>(others.Select(p => p.ProductCode), StringComparer.OrdinalIgnoreCase);
        _existingBarcodes = others
            .Where(p => !string.IsNullOrWhiteSpace(p.Barcode))
            .GroupBy(p => p.Barcode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        ScanBarcodeButton.IsVisible = BarcodeScanner.IsCameraScanAvailable;

        if (editing != null)
        {
            TitleLabel.Text = "Edit Product";
            SaveButton.Text = "Save Changes";
            NameEntry.Text = editing.Name;
            CodeEntry.Text = editing.ProductCode;
            BarcodeEntry.Text = editing.Barcode;
            _suppressCategorySuggestions = true;
            CategoryEntry.Text = editing.CategoryName;
            _suppressCategorySuggestions = false;
            CostEntry.Text = editing.CurrentCostPrice?.ToString("0.##", CultureInfo.InvariantCulture);
            PriceEntry.Text = editing.CurrentSalePrice?.ToString("0.##", CultureInfo.InvariantCulture);
            LowStockEntry.Text = editing.LowStockCount.ToString("0.##", CultureInfo.InvariantCulture);

            // Stock is shown but read-only: the server never takes stock from a device update
            // (a device's figure can be stale), so letting it be edited here would just be a
            // change that silently doesn't stick.
            StockLabel.Text = "Stock on hand";
            StockEntry.Text = editing.CurrentStock.ToString("0.##", CultureInfo.InvariantCulture);
            StockEntry.IsEnabled = false;
            StockHintLabel.IsVisible = true;
        }
        else if (!string.IsNullOrWhiteSpace(prefilledBarcode))
        {
            BarcodeEntry.Text = prefilledBarcode.Trim();
        }
    }

    private async void OnScanBarcodeClicked(object? sender, EventArgs e)
    {
        var scanned = await BarcodeScanner.ScanAsync();
        if (!string.IsNullOrWhiteSpace(scanned))
        {
            BarcodeEntry.Text = scanned;
        }
    }

    private void OnCategoryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressCategorySuggestions) return;

        var term = (e.NewTextValue ?? string.Empty).Trim();
        if (term.Length == 0)
        {
            CategorySuggestionsView.IsVisible = false;
            return;
        }

        var matches = _categories
            .Where(c => c.Contains(term, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(c, term, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .ToList();
        CategorySuggestionsView.ItemsSource = matches;
        CategorySuggestionsView.IsVisible = matches.Count > 0;
    }

    private void OnCategorySuggestionTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string category) return;

        _suppressCategorySuggestions = true;
        CategoryEntry.Text = category;
        _suppressCategorySuggestions = false;
        CategorySuggestionsView.IsVisible = false;
    }

    private static bool TryParseNonNegative(string? text, out decimal value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0;
            return true; // blank means "not provided" — callers decide whether that's allowed
        }
        return decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim() ?? "";
        var code = CodeEntry.Text?.Trim() ?? "";

        if (name.Length == 0) { ShowError("Name is required."); return; }
        if (code.Length == 0) { ShowError("SKU / product code is required."); return; }
        if (_existingCodes.Contains(code)) { ShowError($"A product with SKU \"{code}\" already exists."); return; }

        var barcode = BarcodeEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(barcode) && _existingBarcodes.TryGetValue(barcode, out var owner))
        {
            ShowError($"Barcode {barcode} is already assigned to \"{owner}\".");
            return;
        }

        if (string.IsNullOrWhiteSpace(PriceEntry.Text)) { ShowError("Sale price is required."); return; }
        if (!TryParseNonNegative(PriceEntry.Text, out var price)) { ShowError("Sale price must be a number, 0 or more."); return; }
        if (!TryParseNonNegative(CostEntry.Text, out var cost)) { ShowError("Cost price must be a number, 0 or more."); return; }
        if (!TryParseNonNegative(StockEntry.Text, out var stock)) { ShowError("Initial stock must be a number, 0 or more."); return; }
        if (!TryParseNonNegative(LowStockEntry.Text, out var lowStock)) { ShowError("Low-stock level must be a number, 0 or more."); return; }

        var category = CategoryEntry.Text?.Trim();

        Close(new LocalProduct
        {
            SyncId = _editing?.SyncId ?? Guid.NewGuid(),
            ProductCode = code,
            Barcode = string.IsNullOrEmpty(barcode) ? null : barcode,
            Name = name,
            Description = _editing?.Description ?? "",
            CategoryName = string.IsNullOrEmpty(category) ? null : category,
            CurrentCostPrice = cost,
            CurrentSalePrice = price,
            CurrentStock = _editing?.CurrentStock ?? stock,
            LowStockCount = lowStock,
            // Same relationship the server stores: how far the sale price sits above cost.
            MarkUpPercentage = cost > 0 ? Math.Round((price - cost) / cost * 100m, 2) : 0m,
            IsActive = _editing?.IsActive ?? true,
            LastModifiedUtc = DateTime.UtcNow
        });
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        Close(null);
    }
}
