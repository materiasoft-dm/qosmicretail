using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class RolesManagerTests : BlazorTestBase
{
    public RolesManagerTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RolesList_Loads()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("roles-manager");
        await FirstRow.WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });
        // The seeded roles must be there, not just an empty grid.
        await Assertions.Expect(Page.Locator(".mud-table-body")).ToContainTextAsync("Administrator",
            new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>
    /// Creating a role and ticking permissions is the surviving equivalent of the old MVC Edit page's
    /// "Update Permissions" — the properties under test (a role's page permissions actually persist)
    /// are unchanged. The MVC page's AmbiguousMatchException failure mode has no counterpart here:
    /// a Blazor page is one component with one click handler, not action methods resolved by name.
    /// </summary>
    [Fact]
    public async Task CreateRole_WithSelectedPermission_AppearsInList()
    {
        await LoginAsAdminAsync();
        var name = $"E2E Role {Guid.NewGuid().ToString("N")[..8]}";
        await GotoAppAsync("roles-manager");

        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New Role" }).ClickAsync();
        await FillFieldAsync("Role name", name);
        // The permission checkboxes are the ModuleRegistry page names, e.g. "Product List Page".
        await Page.Locator(".mud-checkbox").Filter(new LocatorFilterOptions { HasTextString = "Product List Page" }).First.ClickAsync();
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).ClickAsync();

        await Assertions.Expect(Page.Locator(".mud-table-body")).ToContainTextAsync(name,
            new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }
}
