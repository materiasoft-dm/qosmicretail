using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests.Blazor;

public class BlazorAdminTests : BlazorTestBase
{
    public BlazorAdminTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    // appsettings.json's AdminQueryApiKey for local/dev runs (see CLAUDE.md) — not a real secret.
    private const string DevAdminQueryKey = "local-dev-only-key-not-a-secret";

    private static string Stamp() => Guid.NewGuid().ToString("N")[..8];

    private ILocator Rows => Page.Locator(".mud-table-body tr.mud-table-row");

    [Fact]
    public async Task AuditTrail_FiltersByAction_AndShowsEntryDetails()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("audit-trail");
        await Rows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });

        await SelectOptionAsync("Action", "LoginSucceeded");
        await Assertions.Expect(Rows.First).ToContainTextAsync("LoginSucceeded", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });

        await Rows.First.ClickAsync();
        var dialog = Page.Locator(".mud-dialog");
        await Assertions.Expect(dialog).ToContainTextAsync("LoginSucceeded");
        await Assertions.Expect(dialog).ToContainTextAsync(TestServerFixture.AdminEmail);
        // The action bar's Close (the corner X is labelled "Close" too).
        await dialog.Locator(".mud-dialog-actions").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Close", Exact = true }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>The raw SQL console needs the admin key on top of the permission, and asks before a write.</summary>
    [Fact]
    public async Task DataQuery_RequiresKey_AndConfirmsWrites()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("data-query");

        await FillFieldAsync("Admin key", "wrong-key");
        await FillFieldAsync("SQL", "SELECT 42 AS answer");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Execute" }).ClickAsync();
        await Assertions.Expect(Page.GetByText("Invalid API key.")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });

        await FillFieldAsync("Admin key", DevAdminQueryKey);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Execute" }).ClickAsync();
        var results = Page.Locator("article.content table");
        await Assertions.Expect(results).ToContainTextAsync("answer", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(results).ToContainTextAsync("42");

        // A write asks first; cancelling runs nothing.
        await FillFieldAsync("SQL", "UPDATE Products SET Note = Note WHERE 1 = 0");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Execute" }).ClickAsync();
        var confirm = Page.Locator(".mud-dialog");
        await Assertions.Expect(confirm).ToContainTextAsync("can't be undone");
        await confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Run it" }).ClickAsync();
        await Assertions.Expect(Page.GetByText("0 row(s) affected")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task DownloadsAndBackupsPages_Load()
    {
        await LoginAsAdminAsync();

        await GotoAppAsync("downloads");
        await Assertions.Expect(Page.Locator("article.content")).ToContainTextAsync("Setting up a Windows terminal", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.Locator("article.content")).ToContainTextAsync("Setting up an Android device");

        // The E2E server runs on SQLite, so daily snapshots exist and are listed.
        await GotoAppAsync("database-backups");
        await Assertions.Expect(Page.Locator("article.content table")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });

        // The seed admin holds Database Administrator, which carries Server Logs.
        await GotoAppAsync("logs");
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Clear all logs" }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>
    /// A platform admin creates a tenant; its own admin can then sign in to a separate, empty
    /// business — but isn't a platform admin, so the Tenants page says "no access" (rather than
    /// bouncing to the login page, which would loop) and isn't offered in the sidebar.
    /// </summary>
    [Fact]
    public async Task PlatformAdmin_CreatesTenant_WhoseAdminCantManageTenants()
    {
        await LoginAsAdminAsync();
        var stamp = Stamp();
        var business = $"E2E Pharmacy {stamp}";
        var email = $"tenant-{stamp}@example.com";
        const string password = "Tenant@12345";

        await GotoAppAsync("platform-admin");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New tenant" }).ClickAsync();
        await FillFieldAsync("Business name", business);
        await FillFieldAsync("First branch", $"Branch {stamp}");
        await FillFieldAsync("Address", "12 Rizal Ave, Manila");
        await FillFieldAsync("Contact number", "0917 555 0101");
        await FillFieldAsync("Email (sign-in)", email);
        await FillFieldAsync("Password", password);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create tenant" }).ClickAsync();
        await Assertions.Expect(Rows.Filter(new LocatorFilterOptions { HasTextString = business })).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });

        await Context.ClearCookiesAsync();
        await LoginAsync(email, password);

        // Its own, isolated business: its first branch, and none of the default tenant's products.
        await GotoAppAsync("products");
        await Assertions.Expect(Page.Locator("#branchSwitcher .branch-name")).ToHaveTextAsync($"Branch {stamp}", new LocatorAssertionsToHaveTextOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.GetByText("No products found.")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });

        // A permission failure is challenged by the cookie auth handler (AccessDeniedPath in
        // Program.cs), so a full page load lands on the account area's "No access" page with the
        // attempted URL kept in ReturnUrl — not on a sign-in page, which is what would loop.
        await Page.GotoAsync("/platform-admin");
        var denied = Page.Locator("#accessDeniedMessage");
        await Assertions.Expect(denied).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = BootTimeoutMs });
        await Assertions.Expect(denied).ToContainTextAsync("You don't have access to that page.");
        Assert.Contains("/account/access-denied", Page.Url);
        Assert.Contains("ReturnUrl", Page.Url);

        // And the app doesn't offer Tenants to a non-platform admin: not in the sidebar, and not
        // among the Settings page's cards (where the platform admin reaches it).
        await GotoAppAsync("settings");
        await Assertions.Expect(Page.Locator("article.content").GetByText("My account", new LocatorGetByTextOptions { Exact = true }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.Locator("article.content").GetByText("Tenants", new LocatorGetByTextOptions { Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(Page.Locator(".sidebar-nav").GetByText("Tenants", new LocatorGetByTextOptions { Exact = true })).ToHaveCountAsync(0);
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>
    /// Data Query, Database Backups and Server Logs reach every tenant's data, so they belong to the
    /// Database Administrator role, not Administrator: a new tenant's admin is neither offered nor
    /// let into them, and can't grant itself that role. It is also created active, so it appears in
    /// its own Users list.
    /// </summary>
    [Fact]
    public async Task TenantAdmin_HasNoDatabaseTools_AndCantGrantDatabaseRole()
    {
        await LoginAsAdminAsync();
        var stamp = Stamp();
        var email = $"tenant-db-{stamp}@example.com";
        const string password = "Tenant@12345";

        await GotoAppAsync("platform-admin");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New tenant" }).ClickAsync();
        await FillFieldAsync("Business name", $"E2E Pharmacy {stamp}");
        await FillFieldAsync("First branch", $"Branch {stamp}");
        await FillFieldAsync("Address", "12 Rizal Ave, Manila");
        await FillFieldAsync("Contact number", "0917 555 0101");
        await FillFieldAsync("Email (sign-in)", email);
        await FillFieldAsync("Password", password);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create tenant" }).ClickAsync();
        await Assertions.Expect(Rows.Filter(new LocatorFilterOptions { HasTextString = $"E2E Pharmacy {stamp}" })).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });

        await Context.ClearCookiesAsync();
        await LoginAsync(email, password);

        await GotoAppAsync("settings");
        var content = Page.Locator("article.content");
        await Assertions.Expect(content.GetByText("Audit Trail", new LocatorGetByTextOptions { Exact = true }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(content.GetByText("Data Query", new LocatorGetByTextOptions { Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(content.GetByText("Database Backups", new LocatorGetByTextOptions { Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(content.GetByText("Logs", new LocatorGetByTextOptions { Exact = true })).ToHaveCountAsync(0);

        var dataQuery = await Page.APIRequest.PostAsync("/api/admin/data-query", new APIRequestContextOptions
        {
            DataObject = new { apiKey = DevAdminQueryKey, sql = "SELECT 1" }
        });
        Assert.True(dataQuery.Status is 401 or 403, $"data-query returned {dataQuery.Status}");
        var backups = await Page.APIRequest.GetAsync("/api/admin/backups");
        Assert.True(backups.Status is 401 or 403, $"backups returned {backups.Status}");
        var logs = await Page.APIRequest.GetAsync("/api/logs");
        Assert.True(logs.Status is 401 or 403, $"logs returned {logs.Status}");

        // Granting itself Database Administrator is refused.
        var users = await (await Page.APIRequest.GetAsync("/api/users")).JsonAsync();
        var myId = users!.Value.GetProperty("items").EnumerateArray()
            .First(u => u.GetProperty("isCurrentUser").GetBoolean()).GetProperty("id").GetString();
        var roles = await (await Page.APIRequest.GetAsync("/api/roles")).JsonAsync();
        var roleIds = roles!.Value.GetProperty("items").EnumerateArray()
            .Where(r => r.GetProperty("name").GetString() is "Administrator" or "Database Administrator")
            .Select(r => r.GetProperty("id").GetString()).ToArray();
        var assign = await Page.APIRequest.PostAsync($"/api/users/{myId}/roles", new APIRequestContextOptions
        {
            DataObject = new { roleIds }
        });
        Assert.Equal(403, assign.Status);
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>
    /// Clicking a tenant's name opens its details (read-only, with Edit and Close); Edit turns the
    /// dialog into a form, and saving updates both the dialog and the list.
    /// </summary>
    [Fact]
    public async Task PlatformAdmin_TenantDetails_ShowAndEdit()
    {
        await LoginAsAdminAsync();
        var stamp = Stamp();
        var business = $"E2E Details Pharmacy {stamp}";

        await GotoAppAsync("platform-admin");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New tenant" }).ClickAsync();
        await FillFieldAsync("Business name", business);
        await FillFieldAsync("First branch", $"Branch {stamp}");
        await FillFieldAsync("Address", "1 Old Street");
        await FillFieldAsync("Contact number", "0917 000 0000");
        await FillFieldAsync("Email (sign-in)", $"details-{stamp}@example.com");
        await FillFieldAsync("Password", "Tenant@12345");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create tenant" }).ClickAsync();

        var link = Page.Locator("button.tenant-link", new PageLocatorOptions { HasTextString = business });
        await link.ClickAsync(new LocatorClickOptions { Timeout = UiTimeoutMs });
        var dialog = Page.Locator(".mud-dialog");
        await Assertions.Expect(dialog).ToContainTextAsync("1 Old Street", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(dialog).ToContainTextAsync("0917 000 0000");
        await Assertions.Expect(dialog.Locator(".mud-dialog-actions").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Close", Exact = true })).ToBeVisibleAsync();

        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Edit", Exact = true }).ClickAsync();
        await FillFieldAsync(Page.Locator("#tenantAddress"), "99 New Avenue, Cebu");
        await FillFieldAsync(Page.Locator("#tenantContact"), "0918 123 4567");
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Save changes" }).ClickAsync();

        // Back to the read-only view, showing the saved values.
        await Assertions.Expect(dialog).ToContainTextAsync("99 New Avenue, Cebu", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(dialog).ToContainTextAsync("0918 123 4567");
        await dialog.Locator(".mud-dialog-actions").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Close", Exact = true }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });

        // The list reloaded with the new contact number.
        await Assertions.Expect(Rows.Filter(new LocatorFilterOptions { HasTextString = business })).ToContainTextAsync("0918 123 4567",
            new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    /// <summary>
    /// The sidebar's branch switcher moves the user to another branch (the classic header's
    /// location selector), and the page reloads showing the new one.
    /// </summary>
    [Fact]
    public async Task BranchSwitcher_SwitchesToAnotherBranch()
    {
        await LoginAsAdminAsync();
        var branch = $"E2E Branch {Stamp()}";

        await GotoAppAsync("locations");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add Location" }).ClickAsync();
        await FillFieldAsync("Branch name", branch);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add branch", Exact = true }).ClickAsync();
        await Assertions.Expect(Page.Locator(".mud-table-body")).ToContainTextAsync(branch, new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });

        await GotoAppAsync("");
        var current = Page.Locator("#branchSwitcher .branch-name");
        await current.WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });
        var original = (await current.InnerTextAsync()).Trim();

        await SwitchBranchAsync(branch);
        await Assertions.Expect(current).ToHaveTextAsync(branch, new LocatorAssertionsToHaveTextOptions { Timeout = BootTimeoutMs });

        // Put this user back where it was, so later tests work in the usual branch.
        await SwitchBranchAsync(original);
        await Assertions.Expect(current).ToHaveTextAsync(original, new LocatorAssertionsToHaveTextOptions { Timeout = BootTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    private async Task SwitchBranchAsync(string name)
    {
        await Page.Locator("#branchSwitcher").ClickAsync();
        await Page.Locator(".branch-option").Filter(new LocatorFilterOptions { HasTextString = name }).ClickAsync();
        await Page.Locator("article.content").WaitForAsync(new LocatorWaitForOptions { Timeout = BootTimeoutMs });
    }
}
