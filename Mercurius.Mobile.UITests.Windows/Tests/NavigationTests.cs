using OpenQA.Selenium;
using Mercurius.Mobile.UITests.Windows.Infrastructure;

namespace Mercurius.Mobile.UITests.Windows.Tests;

public class NavigationTests : MercuriusMobileWindowsTestBase
{
    public NavigationTests(WindowsAppFixture fixture) : base(fixture) { }

    [Theory]
    [InlineData("Sell", "ProductSearchEntry")]
    [InlineData("Receipts", "ReceiptsSummaryLabel")]
    [InlineData("Dashboard", "DashboardGreetingLabel")]
    [InlineData("Products", "ProductCountBadge")]
    [InlineData("Settings", "CurrentClinicLabel")]
    public void FlyoutItem_NavigatesToItsPage(string flyoutTitle, string pageMarkerAutomationId)
    {
        EnsureLoggedIn();
        NavigateTo(flyoutTitle);
        // "Products" specifically has failed here across nearly every real run of this suite — the
        // local dev server's catalog is the developer's whole real product list (hundreds of rows,
        // see CLAUDE.md's Android E2E section), and ProductsPage's own count/load over that appears
        // to genuinely take longer than the other pages' markers. 15s wasn't consistently enough;
        // bumped uniformly rather than special-casing one InlineData row.
        WaitForId(pageMarkerAutomationId, timeoutSeconds: 25);
    }

    // Regression test for the duplicate-header bug: Shell renders its own automatic navigation
    // title bar above every page by default, showing that page's Title property — every page here
    // also renders its own custom in-page header with the same title, so left unfixed, "Sell"
    // rendered twice (once from Shell's chrome, once from SalesPage.xaml's own header). Fixed by
    // AppShell.xaml's Shell.NavBarIsVisible="{OnIdiom ... Desktop=False ...}". Two occurrences of
    // "Sell" here is correct (the flyout's own item label, plus the in-page header) — a third
    // would mean the Shell chrome is back.
    [Fact]
    public void SellPage_ShowsItsTitleExactlyTwice_NotThreeTimes()
    {
        EnsureLoggedIn();
        NavigateTo("Sell");
        WaitForId("ProductSearchEntry", timeoutSeconds: 15);

        var occurrences = Driver.FindElements(By.XPath("//*[@Name='Sell']"));
        Assert.Equal(2, occurrences.Count);
    }
}
