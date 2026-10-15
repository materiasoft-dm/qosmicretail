using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class InvoiceListTests : BlazorTestBase
{
    public InvoiceListTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task InvoicesList_Loads()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("invoices");
        // The grid loads its first page from the API; the empty-state proves the page rendered,
        // a row proves the server round-trip worked too.
        await Page.Locator("article.content .mud-table").WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });
        await Page.Locator("article.content").Locator("visible=true").First.WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });

        var content = await Page.ContentAsync();
        Assert.DoesNotContain("error occurred while processing your request", content, StringComparison.OrdinalIgnoreCase);
        await AssertNoBlazorErrorAsync();
    }
}
