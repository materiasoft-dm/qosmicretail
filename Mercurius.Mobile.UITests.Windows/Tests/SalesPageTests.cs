using System.Linq;
using Mercurius.Mobile.UITests.Windows.Infrastructure;

namespace Mercurius.Mobile.UITests.Windows.Tests;

// Exercises against the two known, price/stock-controlled products seeded for this suite (see
// CLAUDE.md's E2E section):
//   E2E Widget Alpha (E2E-ALPHA-001) — ₱25.00, stock 50, LowStockCount 5  → normal stock
//   E2E Widget Beta  (E2E-BETA-002)  — ₱12.50, stock 0,  LowStockCount 10 → low stock
public class SalesPageTests : MercuriusMobileWindowsTestBase
{
    // Nested labels, not the tiles' own roots (ProductTile_*) — a CollectionView item's root loses
    // its AutomationId on Windows in this MAUI version; see CollectionItems' comment on
    // MercuriusMobileWindowsTestBase.
    private const string AlphaTile = "ProductPrice_E2E-ALPHA-001";
    private const string BetaTile = "ProductPrice_E2E-BETA-002";

    public SalesPageTests(WindowsAppFixture fixture) : base(fixture) { }

    // The view-toggle Border's own AutomationId ("TileViewButton"/"ListViewButton") doesn't
    // forward to its automation peer — confirmed via a real page-source dump: a Border wrapping a
    // single Label exposes only that Label's text-derived Name ("▦ Tiles" / "☰ List"), not the
    // Border's own bound AutomationId. Same family of issue as CollectionItems' comment, different
    // XAML shape (a plain Border, not a CollectionView item) — text is the only reliable handle.
    private const string TilesToggleText = "▦ Tiles";
    private const string ListToggleText = "☰ List";

    private void ClearSearch()
    {
        SetText("ProductSearchEntry", "");
        Thread.Sleep(400); // debounce in SalesPage.xaml.cs's OnSearchTextChanged

        // View mode is persisted via Preferences (survives process relaunches, including
        // WindowsAppFixture's crash-recovery ones — see CLAUDE.md's Windows E2E section) — a prior
        // run left in List view otherwise starts every subsequent run there too. Force Tiles rather
        // than assume it; idempotent (SetViewMode no-ops if already in that mode).
        if (ExistsById("ProductsListView"))
        {
            TapText(TilesToggleText);
            Thread.Sleep(300);
        }
    }

    [Fact]
    public void Search_FiltersToMatchingProductsOnly()
    {
        EnsureLoggedIn();
        ClearSearch();

        SetText("ProductSearchEntry", "E2E Widget");
        WaitForId(AlphaTile); // waits out the search debounce instead of guessing a fixed delay
        Assert.True(ExistsById(BetaTile));

        SetText("ProductSearchEntry", "E2E Widget Alpha");
        WaitUntilGone(BetaTile);
        Assert.True(ExistsById(AlphaTile));

        SetText("ProductSearchEntry", "zzznonexistentproductzzz");
        WaitUntilGone(AlphaTile);

        ClearSearch();
    }

    [Fact]
    public void ProductTiles_ShowCorrectSrpAndStock_InGridView()
    {
        EnsureLoggedIn();
        ClearSearch();
        SetText("ProductSearchEntry", "E2E Widget");
        WaitForId("ProductPrice_E2E-ALPHA-001");

        Assert.True(ExistsById("ProductsGridView"));
        Assert.Equal("₱25.00", ById("ProductPrice_E2E-ALPHA-001").Text);
        Assert.Equal("₱12.50", ById("ProductPrice_E2E-BETA-002").Text);

        // Alpha starts at 50 in stock. CheckoutAndSyncTests and CheckoutTests.CompleteCashSale_
        // ExactAmount each sell exactly one real unit against this same server-side product —
        // since xunit doesn't guarantee class execution order within a collection, Alpha may read
        // anywhere from 50 down to 48 depending on how many of those already ran. Any of those is
        // correct; only a bigger drop or a non-numeric value would indicate a real bug.
        var alphaStock = ById("ProductStock_E2E-ALPHA-001").Text;
        var alphaStockValue = int.Parse(new string(alphaStock.TakeWhile(char.IsDigit).ToArray()));
        Assert.True(alphaStockValue is >= 48 and <= 50, $"Unexpected Alpha stock display: '{alphaStock}'");
        // Beta (0 in stock) is never sold by any test, so this stays exact.
        Assert.Contains("0", ById("ProductStock_E2E-BETA-002").Text);

        ClearSearch();
    }

    [Fact]
    public void ViewToggle_SwitchesBetweenGridAndListWithoutLosingData()
    {
        EnsureLoggedIn();
        ClearSearch();
        SetText("ProductSearchEntry", "E2E Widget");
        WaitForId("ProductPrice_E2E-ALPHA-001");

        // Starts in grid (thumbnail) view.
        Assert.True(ExistsById("ProductsGridView"));
        Assert.False(ExistsById("ProductsListView"));

        // The toggle is a two-segment switch (Tiles | List) — each segment selects that mode
        // explicitly rather than toggling.
        TapText(ListToggleText);
        Thread.Sleep(300);

        Assert.False(ExistsById("ProductsGridView"));
        Assert.True(ExistsById("ProductsListView"));
        // Same underlying data, same SRP/stock values, now in the list template.
        Assert.Equal("₱25.00", ById("ProductPrice_E2E-ALPHA-001").Text);
        Assert.Contains("0", ById("ProductStock_E2E-BETA-002").Text);

        // Switch back — leave the page in its default (grid) state for later tests.
        TapText(TilesToggleText);
        Thread.Sleep(300);
        Assert.True(ExistsById("ProductsGridView"));
        Assert.False(ExistsById("ProductsListView"));

        ClearSearch();
    }
}
