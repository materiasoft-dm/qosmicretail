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
        await Action("Products", "View product list").Locator(".mud-checkbox").ClickAsync();
        await Save();

        await Assertions.Expect(Page.Locator(".mud-table-body")).ToContainTextAsync(name,
            new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>
    /// A page's checkbox ticks or clears everything under it, and shows the mixed state when only
    /// some of its actions are ticked.
    /// </summary>
    [Fact]
    public async Task PageCheckbox_ControlsItsActions_AndShowsMixedState()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("roles-manager");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New Role" }).ClickAsync();

        var products = Group("Products");
        var add = Action("Products", "Add product");
        await ExpectState(products, "unchecked");

        await products.Locator("> .perm-row .mud-checkbox").ClickAsync();
        await ExpectState(products, "checked");
        await ExpectState(add, "checked");
        await ExpectState(Action("Products", "Delete (archive) product"), "checked");

        await add.Locator(".mud-checkbox").ClickAsync();
        await ExpectState(add, "unchecked");
        await ExpectState(products, "mixed");

        // From mixed, the page checkbox ticks everything; a further click clears everything.
        await products.Locator("> .perm-row .mud-checkbox").ClickAsync();
        await ExpectState(products, "checked");
        await ExpectState(add, "checked");
        await products.Locator("> .perm-row .mud-checkbox").ClickAsync();
        await ExpectState(products, "unchecked");
        await ExpectState(add, "unchecked");
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task EditRole_ChangesPermissions_AndTheyPersist()
    {
        await LoginAsAdminAsync();
        var name = $"E2E Edit {Guid.NewGuid().ToString("N")[..8]}";
        await GotoAppAsync("roles-manager");

        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New Role" }).ClickAsync();
        await FillFieldAsync("Role name", name);
        await Action("Products", "View product list").Locator(".mud-checkbox").ClickAsync();
        await Save();

        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = name });
        var edit = row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Edit permissions" });
        await edit.ClickAsync();
        await ExpectState(Action("Products", "View product list"), "checked");
        await ExpectState(Group("Products"), "mixed");

        await Action("Products", "Edit product").Locator(".mud-checkbox").ClickAsync();
        await Action("Products", "View product list").Locator(".mud-checkbox").ClickAsync();
        await Save();
        await Assertions.Expect(Group("Products")).ToHaveCountAsync(0);

        await edit.ClickAsync();
        await ExpectState(Action("Products", "Edit product"), "checked");
        await ExpectState(Action("Products", "View product list"), "unchecked");
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task EditRole_Rename_ShowsNewNameInList()
    {
        await LoginAsAdminAsync();
        var stamp = Guid.NewGuid().ToString("N")[..8];
        await GotoAppAsync("roles-manager");

        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New Role" }).ClickAsync();
        await FillFieldAsync("Role name", $"E2E Before {stamp}");
        await Save();

        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = $"E2E Before {stamp}" });
        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Edit permissions" }).ClickAsync();
        await Assertions.Expect(Field("Role name")).ToHaveValueAsync($"E2E Before {stamp}");
        await FillFieldAsync("Role name", $"E2E After {stamp}");
        await Save();

        await Assertions.Expect(Page.Locator(".mud-table-body")).ToContainTextAsync($"E2E After {stamp}",
            new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(row).ToHaveCountAsync(0);
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task AdministratorRole_CannotBeEdited()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("roles-manager");
        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = "Always has every permission" });
        await Assertions.Expect(row).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Edit permissions" })).ToHaveCountAsync(0);
    }

    private ILocator Group(string label) => Page.Locator($".perm-root > [data-node='{label}']");

    private ILocator Action(string group, string label) => Group(group).Locator($"[data-node='{label}']");

    private Task Save() => Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).ClickAsync();

    private static Task ExpectState(ILocator node, string state) =>
        Assertions.Expect(node).ToHaveAttributeAsync("data-state", state, new LocatorAssertionsToHaveAttributeOptions { Timeout = UiTimeoutMs });
}
