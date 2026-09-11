using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class ProductsTests : MercuriusTestBase
{
    public ProductsTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ProductsList_Loads()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Products");

        await Page.Locator("#productsTable").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        // DataTables populates rows via AJAX after initial render — wait for at least one data
        // row rather than just the empty table shell.
        await Page.Locator("#productsTable tbody tr").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task CreateProduct_ThenAppearsInList()
    {
        await LoginAsAdminAsync();
        var uniqueName = await Page.CreateTestProductAsync("E2E Test Product");

        await Page.GotoAsync("/Products");
        // DataTables' search box only reacts to a real 'keyup' event; Fill() sets the value
        // directly without dispatching one, so type it out one keystroke at a time instead.
        await Page.Locator("#searchInput").PressSequentiallyAsync(uniqueName, new LocatorPressSequentiallyOptions { Delay = 20 });
        await Page.Locator($"#productsTable tbody tr:has-text('{uniqueName}')").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    /// <summary>
    /// "Delete" is a soft delete throughout this app (see CLAUDE.md) — the row must still exist
    /// afterward, just flipped to IsActive = false, not removed from the list.
    /// </summary>
    [Fact]
    public async Task DeleteProduct_IsSoftDeleted_StaysInListAsInactive()
    {
        await LoginAsAdminAsync();
        var productName = await Page.CreateTestProductAsync("E2E Delete Product");

        await Page.GotoAsync("/Products");
        await Page.Locator("#searchInput").PressSequentiallyAsync(productName, new LocatorPressSequentiallyOptions { Delay = 20 });
        var row = Page.Locator($"#productsTable tbody tr:has-text('{productName}')");
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await Assertions.Expect(row).ToContainTextAsync("Active");

        await row.Locator("a[title=Delete]").ClickAsync();
        await Page.WaitForURLAsync(url => url.Contains("/Products/Delete/"), new PageWaitForURLOptions { Timeout = 10000 });

        await Page.ClickAsync("input[type=submit][value=Delete]");
        await Page.WaitForURLAsync(url => url.Contains("/Products") && !url.Contains("Delete"), new PageWaitForURLOptions { Timeout = 10000 });

        await Page.Locator("#searchInput").PressSequentiallyAsync(productName, new LocatorPressSequentiallyOptions { Delay = 20 });
        row = Page.Locator($"#productsTable tbody tr:has-text('{productName}')");
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await Assertions.Expect(row).ToContainTextAsync("Inactive");
    }

    // A minimal but fully valid 1x1 transparent PNG, embedded here rather than as a file on disk so
    // this test doesn't depend on an unrelated theme asset that could be renamed/removed later.
    // Server-side upload validation (ProductsController.SaveProductImageAsync) only checks the file
    // extension, not real image content, so this is sufficient to exercise the actual upload path.
    private static readonly byte[] MinimalPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    /// <summary>
    /// Regression test for the Details page's blank-image fallback and Edit page's upload flow:
    /// a product with no photo must show the blank-image placeholder (not a broken-image icon),
    /// and after uploading a photo via Edit, Details must switch to showing the real file.
    /// </summary>
    [Fact]
    public async Task ProductPhoto_ShowsPlaceholder_ThenActualImageAfterUpload()
    {
        await LoginAsAdminAsync();
        var productName = await Page.CreateTestProductAsync("E2E Photo Product");

        await Page.GotoAsync("/Products");
        await Page.Locator("#searchInput").PressSequentiallyAsync(productName, new LocatorPressSequentiallyOptions { Delay = 20 });
        var row = Page.Locator($"#productsTable tbody tr:has-text('{productName}')");
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        await row.Locator("a[title=View]").ClickAsync();
        await Page.WaitForURLAsync(url => url.Contains("/Products/Details/"), new PageWaitForURLOptions { Timeout = 10000 });
        await Assertions.Expect(Page.Locator("img[alt='No photo set']")).ToHaveAttributeAsync("src", "/assets/media/svg/files/blank-image.svg");

        var detailsUrl = Page.Url;
        var productId = detailsUrl.Substring(detailsUrl.LastIndexOf('/') + 1);

        await Page.GotoAsync($"/Products/Edit/{productId}");
        await Page.SetInputFilesAsync("input[type=file][name=productimage]", new FilePayload
        {
            Name = "test-photo.png",
            MimeType = "image/png",
            Buffer = MinimalPng
        });
        await Page.ClickAsync("button[type=submit]:has-text('Submit')");
        // Edit is an ajax-form redirecting back to /Products — wait for the actual navigation via
        // a DOM signal, not a URL check, since the target list page is reachable from many states.
        await Page.WaitForURLAsync(url => url.Contains("/Products") && !url.Contains("/Edit/"), new PageWaitForURLOptions { Timeout = 10000 });

        await Page.GotoAsync($"/Products/Details/{productId}");
        var img = Page.Locator("img.img-thumbnail.img-150");
        await Assertions.Expect(img).Not.ToHaveAttributeAsync("src", "/assets/media/svg/files/blank-image.svg");
        var src = await img.GetAttributeAsync("src");
        Assert.NotNull(src);
        Assert.StartsWith("/productimages/", src);
    }
}
