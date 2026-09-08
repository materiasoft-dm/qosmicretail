using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class InvoiceListTests : MercuriusTestBase
{
    public InvoiceListTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task InvoicesList_Loads()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Invoices");
        await Page.Locator("#invoicesTable").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.DoesNotContain("error occurred while processing your request", content, StringComparison.OrdinalIgnoreCase);
    }
}
