using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Infrastructure;

/// <summary>
/// Base class for all E2E test classes. xUnit constructs a new instance of the test class per
/// test method, so IAsyncLifetime here gives each test its own isolated browser context (own
/// cookies/session) against the one shared server + browser process for the whole run.
/// </summary>
[Collection(MercuriusCollection.Name)]
public abstract class MercuriusTestBase : IAsyncLifetime
{
    protected readonly MercuriusCollectionFixture Fixture;
    protected string BaseUrl => Fixture.Server.BaseUrl;
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    protected MercuriusTestBase(MercuriusCollectionFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        Context = await Fixture.Browser.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true,
        });
        Page = await Context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.CloseAsync();
    }

    /// <summary>Logs in through the real UI (not a cookie shortcut) so every test also exercises
    /// the actual login flow.</summary>
    protected async Task LoginAsAdminAsync()
    {
        await Page.GotoAsync("/Identity/Account/Login");
        await Page.FillAsync("#Input_Email", TestServerFixture.AdminEmail);
        await Page.FillAsync("#Input_Password", TestServerFixture.AdminPassword);
        await Page.ClickAsync("#kt_sign_in_submit");
        await Page.WaitForURLAsync(url => !url.Contains("/Identity/Account/Login"), new PageWaitForURLOptions { Timeout = 15000 });
    }

    /// <summary>Waits for a toastr flash message (success/error/info) to appear and returns its text.</summary>
    protected async Task<string> WaitForToastAsync(int timeoutMs = 10000)
    {
        var toast = Page.Locator(".toast-message").First;
        await toast.WaitForAsync(new LocatorWaitForOptions { Timeout = timeoutMs });
        return await toast.InnerTextAsync();
    }
}
