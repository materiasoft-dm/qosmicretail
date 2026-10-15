using CommunityToolkit.Maui.Views;

namespace Mercurius.Mobile;

// Searchable category "dropdown" for SalesPage — MAUI's stock Picker has no built-in filter box,
// so this is a small popup (same pattern as QuantityPopup: modal, returns a result via Close()).
// Filters the list as the cashier types; tapping a row selects it immediately (no separate OK
// button needed since a row tap is already an unambiguous, single choice).
public partial class CategoryPickerPopup : Popup
{
    private readonly List<string> _allCategories;

    public CategoryPickerPopup(List<string> categories)
    {
        InitializeComponent();
        _allCategories = categories;
        CategoriesListView.ItemsSource = _allCategories;
    }

    private void OnCategorySearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var term = (e.NewTextValue ?? string.Empty).Trim();
        CategoriesListView.ItemsSource = string.IsNullOrEmpty(term)
            ? _allCategories
            : _allCategories.Where(c => c.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnCategoryRowTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string category)
        {
            Close(category);
        }
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        Close(null);
    }
}
