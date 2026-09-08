using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

/// <summary>
/// RolesManagerController used to have duplicate Create/Edit/Delete action methods (one plain,
/// one with an added CancellationToken parameter), which threw an AmbiguousMatchException on
/// every single hit to those pages. See CLAUDE.md / the fix commit. These tests exist
/// specifically to catch that regression if it's ever reintroduced.
/// </summary>
public class RolesManagerTests : MercuriusTestBase
{
    public RolesManagerTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RolesList_Loads()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/RolesManager");

        await Page.Locator("table tbody tr").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task EditRole_LoadsWithoutAmbiguousMatchError()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/RolesManager");

        var editLink = Page.Locator("a[title='Edit']").First;
        await editLink.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await editLink.ClickAsync();

        await Page.WaitForURLAsync(url => url.Contains("/RolesManager/Edit"), new PageWaitForURLOptions { Timeout = 10000 });

        var content = await Page.ContentAsync();
        Assert.DoesNotContain("AmbiguousMatchException", content);
        Assert.DoesNotContain("error occurred while processing your request", content, StringComparison.OrdinalIgnoreCase);
        await Page.Locator("#roleEditForm").WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
    }

    [Fact]
    public async Task EditRole_SubmitPermissions_Succeeds()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/RolesManager");

        var editLink = Page.Locator("a[title='Edit']").First;
        await editLink.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await editLink.ClickAsync();
        await Page.WaitForURLAsync(url => url.Contains("/RolesManager/Edit"), new PageWaitForURLOptions { Timeout = 10000 });

        await Page.ClickAsync("button:has-text('Update Permissions')");

        await Page.WaitForURLAsync(url => url.Contains("/RolesManager") && !url.Contains("Edit"), new PageWaitForURLOptions { Timeout = 10000 });
        var content = await Page.ContentAsync();
        Assert.DoesNotContain("AmbiguousMatchException", content);
    }
}
