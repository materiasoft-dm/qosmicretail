using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class CustomersTests : BlazorTestBase
{
    public CustomersTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CustomersList_Loads()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("customers");
        // The grid is server-paged; on a fresh test database it renders its empty state rather than
        // rows, so assert that the grid itself resolved and that nothing failed server-side.
        await Page.Locator("article.content .mud-table").WaitForAsync(new LocatorWaitForOptions { Timeout = BootTimeoutMs });
        await Assertions.Expect(Page.Locator("article.content")).Not.ToContainTextAsync(
            "error occurred while processing your request",
            new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task CreateCustomer_ThenAppearsInList()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("customers");

        var firstName = $"E2E{Guid.NewGuid():N}"[..10];
        const string lastName = "TestPatient";

        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add Customer", Exact = true }).ClickAsync();
        await FillFieldAsync("First name", firstName);
        await FillFieldAsync("Last name", lastName);
        // The dialog's own submit button is "Add customer" — only case differs from the page's, so
        // scope it to the dialog and match exactly.
        await Page.Locator(".mud-dialog").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add customer", Exact = true })
            .ClickAsync();

        var toast = await WaitForToastAsync();
        Assert.Contains("Customer added.", toast);

        // The grid is server-paged and server-filtered, so find the new row through its own search.
        await SearchAsync("Search by name, phone, email or ID...", firstName);
        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = firstName });
        await Assertions.Expect(row).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(row).ToContainTextAsync(lastName);
        await AssertNoBlazorErrorAsync();
    }
}
