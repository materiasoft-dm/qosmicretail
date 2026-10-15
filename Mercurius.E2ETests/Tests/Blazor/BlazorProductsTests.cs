using System.Text;
using System.Text.RegularExpressions;
using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests.Blazor;

public class BlazorProductsTests : BlazorTestBase
{
    public BlazorProductsTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    private static readonly byte[] MinimalPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static string Stamp() => Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task ProductList_LoadsAndSortsByPrice()
    {
        await LoginAsAdminAsync();
        await CreateStampedProductAsync("Blazor Sort", Stamp());

        await GotoAppAsync("products");
        await Page.Locator(".mud-table-body tr.mud-table-row").First.WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });

        // Sorting by a decimal column must work on SQLite too (it can't ORDER BY decimal natively).
        var reload = Page.WaitForResponseAsync(r => r.Url.Contains("/api/products?") && r.Url.Contains("sort=price"));
        await Page.Locator(".mud-table-sort-label").Filter(new LocatorFilterOptions { HasTextString = "Sale price" }).ClickAsync();
        var response = await reload;
        Assert.Equal(200, response.Status);
        await Page.Locator(".mud-table-body tr.mud-table-row").First.WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>
    /// The full Add form: pricing auto-calc, barcode, a category's custom fields and a photo, then
    /// the details page shows all of it — and the list finds the product by its barcode.
    /// </summary>
    [Fact]
    public async Task CreateProduct_WithBarcodeCustomFieldsAndPhoto_ShowsOnDetails()
    {
        await LoginAsAdminAsync();
        var stamp = Stamp();
        var name = $"Blazor Full {stamp}";
        var barcode = $"480{stamp}";

        await GotoAppAsync("products/new");
        await FillFieldAsync("Item name", name);
        await FillFieldAsync("SKU", $"BLZ-{stamp}");
        await FillFieldAsync("Barcode", barcode);
        await FillFieldAsync("Cost price", "10");
        await FillFieldAsync("Markup %", "50");
        await Assertions.Expect(Field("Sale price")).ToHaveValueAsync("15.00");

        await SelectOptionAsync("Category", "Baby Care");
        await FillFieldAsync("Material", "Cotton");
        await Page.Locator(".mud-checkbox").Filter(new LocatorFilterOptions { HasTextString = "Hypoallergenic" }).ClickAsync();

        await Page.SetInputFilesAsync("input[type=file]", new FilePayload { Name = "photo.png", MimeType = "image/png", Buffer = MinimalPng });
        await Assertions.Expect(Page.GetByText("photo.png is uploaded when you save.")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create product" }).ClickAsync();
        await Page.WaitForURLAsync(new Regex(@"/products/\d+$"), new PageWaitForURLOptions { Timeout = UiTimeoutMs });

        var content = Page.Locator("article.content");
        await Assertions.Expect(content).ToContainTextAsync(name);
        await Assertions.Expect(content).ToContainTextAsync(barcode);
        await Assertions.Expect(content).ToContainTextAsync("Baby Care");
        await Assertions.Expect(content).ToContainTextAsync("Cotton");
        await Assertions.Expect(content).ToContainTextAsync("₱15.00");

        var photo = Page.Locator($"img[alt='{name}']");
        var src = await photo.GetAttributeAsync("src");
        Assert.StartsWith("/ProductImages/", src);
        await Page.WaitForFunctionAsync(
            "() => { const i = document.querySelector(\"img[alt='QR code']\"); return !!i && i.complete && i.naturalWidth > 0; }",
            null, new PageWaitForFunctionOptions { Timeout = UiTimeoutMs });

        await GotoAppAsync("products");
        await Page.GetByLabel("Search by name, SKU or barcode...").FillAsync(barcode);
        await Assertions.Expect(Page.Locator(".mud-table-body tr.mud-table-row")).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.Locator(".mud-table-body tr.mud-table-row").First).ToContainTextAsync(name);
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>"Delete" is a soft delete: switching a product off keeps it listed, as Inactive.</summary>
    [Fact]
    public async Task EditProduct_Deactivate_StaysListedAsInactive()
    {
        await LoginAsAdminAsync();
        var stamp = Stamp();
        var name = await CreateStampedProductAsync("Blazor Edit", stamp);

        await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Edit" }).ClickAsync();
        await Page.WaitForURLAsync(new Regex(@"/products/\d+/edit$"), new PageWaitForURLOptions { Timeout = UiTimeoutMs });
        await FillFieldAsync("Item name", name + " v2");
        await Page.Locator(".mud-switch").First.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save changes" }).ClickAsync();
        await Page.WaitForURLAsync(new Regex(@"/products/\d+$"), new PageWaitForURLOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.Locator("article.content")).ToContainTextAsync(name + " v2");
        await Assertions.Expect(Page.Locator("article.content .mud-chip").First).ToHaveTextAsync("Inactive");

        await GotoAppAsync("products");
        await Page.GetByLabel("Search by name, SKU or barcode...").FillAsync($"BLZ-{stamp}");
        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = name });
        await Assertions.Expect(row).ToContainTextAsync("Inactive", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>
    /// CSV import runs on a background service with no signed-in user of its own; the imported
    /// products must still land in the uploader's tenant (they were invisible before that fix).
    /// </summary>
    [Fact]
    public async Task ImportCsv_Completes_AndProductsAreListed()
    {
        await LoginAsAdminAsync();
        var stamp = Stamp();
        var csv = "SKU,Name,Category,Cost,Price [Main Store],In stock [Main Store]\n" +
                  $"IMP-{stamp}-1,Imported One {stamp},Imported {stamp},1.50,3.00,7\n" +
                  $"IMP-{stamp}-2,Imported Two {stamp},,2,4,0\n";

        await GotoAppAsync("products/import");
        await Page.SetInputFilesAsync("input[type=file]", new FilePayload { Name = "products.csv", MimeType = "text/csv", Buffer = Encoding.UTF8.GetBytes(csv) });
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Import products" }).ClickAsync();
        var summary = Page.Locator(".mud-alert-message").Filter(new LocatorFilterOptions { HasTextString = "Created 2, updated 0, skipped 0, errors 0." });
        await Assertions.Expect(summary).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });

        await GotoAppAsync("products");
        await Page.GetByLabel("Search by name, SKU or barcode...").FillAsync($"IMP-{stamp}");
        await Assertions.Expect(Page.Locator(".mud-table-body tr.mud-table-row")).ToHaveCountAsync(2, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        var first = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = $"Imported One {stamp}" });
        await Assertions.Expect(first).ToContainTextAsync("₱3.00");
        await Assertions.Expect(first).ToContainTextAsync($"Imported {stamp}");
        await AssertNoBlazorErrorAsync();
    }

    private async Task<string> CreateStampedProductAsync(string prefix, string stamp)
    {
        var name = $"{prefix} {stamp}";
        await CreateProductViaFormAsync(name, $"BLZ-{stamp}");
        return name;
    }
}
