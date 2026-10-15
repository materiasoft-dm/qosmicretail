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

    /// <summary>
    /// Logs in through the real UI (not a cookie shortcut) so every test also exercises the
    /// actual login flow.
    /// </summary>
    protected Task LoginAsAdminAsync() => LoginAsync(TestServerFixture.AdminEmail, TestServerFixture.AdminPassword);

    /// <summary>The current path of the app's sign-in page (a server-rendered Blazor form, not MVC).</summary>
    protected const string LoginPath = "/account/login";

    protected async Task LoginAsync(string email, string password)
    {
        await Page.GotoAsync(LoginPath);
        await Page.FillAsync("#email", email);
        await Page.FillAsync("#password", password);
        await Page.ClickAsync("#loginSubmit");
        // A successful sign-in navigates away from the login form; a bad one re-renders it with
        // the error banner, so this is also the signal that a *successful* login happened.
        await Page.WaitForURLAsync(url => !url.Contains(LoginPath), new PageWaitForURLOptions { Timeout = 30000 });
    }

    /// <summary>
    /// Waits for a MudBlazor snackbar (the app's flash messages) to appear and returns its text.
    /// </summary>
    protected async Task<string> WaitForToastAsync(int timeoutMs = 10000)
    {
        var toast = Page.Locator(".mud-snackbar").First;
        await toast.WaitForAsync(new LocatorWaitForOptions { Timeout = timeoutMs });
        return (await toast.InnerTextAsync()).Trim();
    }
}
