using Microsoft.Playwright;

namespace Mercurius.E2ETests.Infrastructure;

/// <summary>
/// Helpers for tests that drive the Blazor WebAssembly app (MudBlazor components) served from the
/// site root. Pages are prerendered by the host and then run on WebAssembly, so tests wait for
/// <c>data-interactive</c> before touching anything — see <see cref="GotoAppAsync"/>.
/// </summary>
public abstract class BlazorTestBase : MercuriusTestBase
{
    // The first load of a fresh browser context downloads and boots the WASM runtime, which is
    // much slower than a server-rendered page; navigations inside the app afterwards are instant.
    protected const int BootTimeoutMs = 60000;
    protected const int UiTimeoutMs = 15000;

    protected BlazorTestBase(MercuriusCollectionFixture fixture) : base(fixture) { }

    /// <summary>
    /// Loads a page and waits until the Blazor app is actually interactive.
    /// </summary>
    /// <remarks>
    /// The app is served from the site root (not /app — LegacyRedirects 301s the old /app paths
    /// here). MainLayout flips <c>.page</c>'s <c>data-interactive</c> to "true" once WebAssembly has
    /// taken over from the prerendered HTML; before that the page is on screen but its buttons do
    /// nothing, so waiting on <c>article.content</c> alone is a race.
    /// </remarks>
    protected async Task GotoAppAsync(string path)
    {
        await Page.GotoAsync("/" + path.TrimStart('/'));
        await WaitForInteractiveAsync();
    }

    /// <summary>Waits for the current Blazor page to finish booting on WebAssembly.</summary>
    protected Task WaitForInteractiveAsync() =>
        Page.Locator(".page[data-interactive='true']").WaitForAsync(new LocatorWaitForOptions { Timeout = BootTimeoutMs });

    /// <summary>A MudBlazor text/numeric field, found by its floating label.</summary>
    protected ILocator Field(string label) => Page.GetByLabel(label).First;

    /// <summary>The first MudBlazor data-table row, once the table has loaded rows.</summary>
    protected ILocator FirstRow => Page.Locator(".mud-table-body tr.mud-table-row").First;

    /// <summary>Types into a search box that filters a MudBlazor table server-side.</summary>
    protected async Task SearchAsync(string label, string term)
    {
        await Field(label).FillAsync(term);
        // The pages debounce the search before re-querying the server.
        await Page.WaitForTimeoutAsync(400);
    }

    /// <summary>
    /// Fills a MudBlazor field and tabs out of it. Fields that aren't Immediate only commit their
    /// value on change/blur, so a bare Fill would leave the component's bound value stale.
    /// </summary>
    protected Task FillFieldAsync(string label, string value) => FillFieldAsync(Field(label), value);

    protected async Task FillFieldAsync(ILocator input, string value)
    {
        await input.WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });
        await input.FillAsync(value);
        await input.PressAsync("Tab");
    }

    /// <summary>Opens a MudSelect by its label and picks an option by its text.</summary>
    protected async Task SelectOptionAsync(string label, string option)
    {
        await Page.Locator(".mud-select").Filter(new LocatorFilterOptions { Has = Page.Locator($"label:has-text('{label}')") })
            .First.ClickAsync();
        await Page.Locator(".mud-popover-open .mud-list-item").Filter(new LocatorFilterOptions { HasTextString = option })
            .First.ClickAsync();
    }

    /// <summary>Types into a MudAutocomplete and picks the first suggestion containing <paramref name="match"/>.</summary>
    protected async Task PickAutocompleteAsync(ILocator input, string search, string match)
    {
        await input.ClickAsync();
        await input.PressSequentiallyAsync(search, new LocatorPressSequentiallyOptions { Delay = 30 });
        await Page.Locator(".mud-popover-open .mud-list-item").Filter(new LocatorFilterOptions { HasTextString = match })
            .First.ClickAsync(new LocatorClickOptions { Timeout = UiTimeoutMs });
    }

    /// <summary>Creates a product through the Blazor Add form; leaves the browser on its details page.</summary>
    protected async Task CreateProductViaFormAsync(string name, string sku, string cost = "5", string markup = "100")
    {
        await GotoAppAsync("products/new");
        await FillFieldAsync("Item name", name);
        await FillFieldAsync("SKU", sku);
        await FillFieldAsync("Cost price", cost);
        await FillFieldAsync("Markup %", markup);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create product" }).ClickAsync();
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/products/\d+$"), new PageWaitForURLOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.Locator("article.content")).ToContainTextAsync(name);
    }

    /// <summary>
    /// Creates a product through the Blazor Add form; leaves the browser on its details page.
    /// Returns the product's display name.
    /// </summary>
    protected async Task<string> CreateTestProductAsync(string namePrefix = "E2E Product")
    {
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var name = $"{namePrefix} {stamp}";
        await CreateProductViaFormAsync(name, $"E2E-{stamp}");
        return name;
    }

    /// <summary>
    /// Creates a supplier through the Suppliers page's Add dialog. Returns the supplier's name.
    /// </summary>
    protected async Task<string> CreateTestSupplierAsync(string namePrefix = "E2E Supplier")
    {
        var name = $"{namePrefix} {Guid.NewGuid().ToString("N")[..8]}";
        await GotoAppAsync("suppliers");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add Supplier" }).ClickAsync();
        await FillFieldAsync("Supplier name", name);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).ClickAsync();
        await Assertions.Expect(Page.Locator(".mud-table-body")).ToContainTextAsync(name,
            new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        return name;
    }

    /// <summary>Blazor shows #blazor-error-ui whenever a component throws an unhandled exception.</summary>
    protected Task AssertNoBlazorErrorAsync() =>
        Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
}
