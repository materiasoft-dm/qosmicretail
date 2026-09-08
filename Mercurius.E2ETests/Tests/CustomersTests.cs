using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class CustomersTests : MercuriusTestBase
{
    public CustomersTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CustomersList_Loads()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Customers");
        await Page.Locator("#customersTable").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task CreateCustomer_ThenAppearsInList()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Customers/Create");

        var firstName = $"E2E{Guid.NewGuid():N}".Substring(0, 10);
        var lastName = "TestPatient";

        await Page.FillAsync("#FirstName", firstName);
        await Page.FillAsync("#LastName", lastName);
        await Page.ClickAsync("button:has-text('Save')");

        await Page.WaitForURLAsync(url => url.Contains("/Customers") && !url.Contains("Create"), new PageWaitForURLOptions { Timeout = 10000 });

        // Index renders via DataTables AJAX, not server-side, so the redirect response itself
        // won't contain the new row yet — search for it once the table has loaded. The search
        // box only reacts to a real 'keyup' event, which Fill() doesn't dispatch.
        await Page.Locator("#searchInput").PressSequentiallyAsync(firstName, new LocatorPressSequentiallyOptions { Delay = 20 });
        await Page.Locator($"#customersTable tbody tr:has-text('{firstName}')").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }
}
