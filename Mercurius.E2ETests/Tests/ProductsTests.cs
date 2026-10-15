using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class ProductsTests : BlazorTestBase
{
    public ProductsTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ProductsList_Loads()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("products");
        // Rows come from the server on first paint, so waiting for one proves the round-trip.
        await FirstRow.WaitForAsync(new LocatorWaitForOptions { Timeout = BootTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task CreateProduct_ThenAppearsInList()
    {
        await LoginAsAdminAsync();
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var name = $"E2E Test Product {stamp}";
        await CreateProductViaFormAsync(name, $"E2E-L-{stamp}");

        await GotoAppAsync("products");
        await SearchAsync("Search by name, SKU or barcode...", name);
        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = name });
        await Assertions.Expect(row).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(row).ToContainTextAsync("Active");
        await AssertNoBlazorErrorAsync();
    }

    // A minimal but fully valid 1x1 transparent PNG, embedded rather than read from disk so this
    // test doesn't depend on an unrelated theme asset. Server-side upload validation only checks
    // the extension, not real image content, so this exercises the actual upload path.
    private static readonly byte[] MinimalPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    /// <summary>
    /// A product with no photo must show the placeholder, not a broken image; after uploading one
    /// through Edit, the details page must show the real file.
    /// </summary>
    [Fact]
    public async Task ProductPhoto_ShowsPlaceholder_ThenActualImageAfterUpload()
    {
        await LoginAsAdminAsync();
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var name = $"E2E Photo Product {stamp}";
        await CreateProductViaFormAsync(name, $"E2E-P-{stamp}");

        // No photo yet: the details page falls back to a placeholder, so there is no photo <img>.
        var img = Page.Locator($"article.content img[alt='{name}']");
        await Assertions.Expect(img).ToHaveCountAsync(0);

        await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Edit" }).ClickAsync();
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/products/\d+/edit$"),
            new PageWaitForURLOptions { Timeout = UiTimeoutMs });
        await Page.SetInputFilesAsync("input[type=file]", new FilePayload
        {
            Name = "test-photo.png",
            MimeType = "image/png",
            Buffer = MinimalPng
        });
        await Assertions.Expect(Page.GetByText("test-photo.png is uploaded when you save."))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save changes" }).ClickAsync();
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/products/\d+$"),
            new PageWaitForURLOptions { Timeout = UiTimeoutMs });

        await Assertions.Expect(img).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        var src = await img.GetAttributeAsync("src");
        Assert.NotNull(src);
        Assert.StartsWith("/ProductImages/", src, StringComparison.OrdinalIgnoreCase);
        await AssertNoBlazorErrorAsync();
    }
}
