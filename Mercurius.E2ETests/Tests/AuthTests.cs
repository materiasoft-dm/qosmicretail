using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

public class AuthTests : MercuriusTestBase
{
    public AuthTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Login_WithValidCredentials_RedirectsToDashboard()
    {
        await LoginAsAdminAsync();

        Assert.DoesNotContain("/Identity/Account/Login", Page.Url);
        await Page.Locator("text=Dashboard").First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShowsError()
    {
        await Page.GotoAsync("/Identity/Account/Login");
        await Page.FillAsync("#Input_Email", TestServerFixture.AdminEmail);
        await Page.FillAsync("#Input_Password", "WrongPassword123!");
        await Page.ClickAsync("#kt_sign_in_submit");

        await Page.Locator("text=Invalid login attempt").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        Assert.Contains("/Identity/Account/Login", Page.Url);
    }

    [Fact]
    public async Task Logout_ReturnsToLoginPage()
    {
        await LoginAsAdminAsync();

        // The sidebar's logout is a POST form in most Identity-scaffolded layouts; fall back to
        // navigating directly to the logout endpoint if no visible link is found, since the
        // point of this test is verifying the session actually ends, not how the button is drawn.
        var logoutLink = Page.Locator("a:has-text('Logout'), a:has-text('Sign out'), button:has-text('Logout')").First;
        if (await logoutLink.CountAsync() > 0)
        {
            await logoutLink.ClickAsync();
        }
        else
        {
            await Page.GotoAsync("/Identity/Account/Logout");
        }

        await Page.GotoAsync("/Products");
        await Page.WaitForURLAsync(url => url.Contains("/Identity/Account/Login"), new PageWaitForURLOptions { Timeout = 10000 });
    }
}
