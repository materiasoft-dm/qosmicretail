using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class AuthTests : BlazorTestBase
{
    public AuthTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Login_WithValidCredentials_RedirectsToDashboard()
    {
        await LoginAsAdminAsync();

        Assert.DoesNotContain(LoginPath, Page.Url);
        await GotoAppAsync("");
        await Assertions.Expect(Page).ToHaveTitleAsync(
            new System.Text.RegularExpressions.Regex("Dashboard", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShowsError()
    {
        await Page.GotoAsync(LoginPath);
        await Page.FillAsync("#email", TestServerFixture.AdminEmail);
        await Page.FillAsync("#password", "WrongPassword123!");
        await Page.ClickAsync("#loginSubmit");

        // The login form posts back to itself and renders its own error banner; it must not
        // navigate away, and must certainly not sign anyone in.
        var error = Page.Locator(".auth-alert");
        await Assertions.Expect(error).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 15000 });
        await Assertions.Expect(error).ToContainTextAsync("don't match");
        Assert.Contains(LoginPath, Page.Url);

        // Still signed out: an app page bounces straight back to the sign-in form.
        await Page.GotoAsync("/products");
        await Page.WaitForURLAsync(url => url.Contains(LoginPath), new PageWaitForURLOptions { Timeout = 15000 });
    }

    [Fact]
    public async Task Logout_ReturnsToLoginPage()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("");

        // The account menu's "Log out" is a real form POST (antiforgery-protected) that clears the
        // sign-in cookie on the server. Reaching it through the menu is deliberate: this menu used to
        // be a MudMenu whose activator never opened, which left "Log out" and "Account settings"
        // unreachable for every user (see MainLayout).
        await Page.Locator("#userMenu").ClickAsync();
        await Page.Locator("#logoutButton").ClickAsync();
        await Page.WaitForURLAsync(url => url.Contains(LoginPath), new PageWaitForURLOptions { Timeout = 15000 });

        // And the session really is gone — an app page bounces back to sign-in.
        await Page.GotoAsync("/products");
        await Page.WaitForURLAsync(url => url.Contains(LoginPath), new PageWaitForURLOptions { Timeout = 15000 });
    }
}
