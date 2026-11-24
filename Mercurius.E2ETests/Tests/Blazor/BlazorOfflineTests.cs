using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests.Blazor;

/// <summary>
/// The installed app has to keep working when the internet drops: open with no connection (the
/// service worker serves the saved page and app files), sell from the device's catalog, keep the sale
/// in the outbox, and send it by itself once the connection is back.
/// </summary>
public class BlazorOfflineTests : BlazorTestBase
{
    public BlazorOfflineTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    private const int OfflineTimeoutMs = 30000;

    [Fact]
    public async Task AppOpensOffline_SellsFromTheDeviceCatalog_AndSendsTheSaleWhenBackOnline()
    {
        await LoginAsAdminAsync();
        var productName = await CreateTestProductAsync("E2E Offline Product");

        // Online first: the service worker takes control, saves the Sell page and the app's files,
        // and the device's catalog (with the new product) is pulled.
        await GotoAppAsync("sell");
        await Page.WaitForFunctionAsync("() => navigator.serviceWorker && navigator.serviceWorker.controller !== null",
            null, new PageWaitForFunctionOptions { Timeout = OfflineTimeoutMs });
        // A reload under the worker's control, so every app file passes through it and is saved.
        await Page.ReloadAsync();
        await WaitForInteractiveAsync();
        await WaitUntilAsync($"async () => (await window.mercuriusOfflineStore.getAllProducts()).some(p => p.name === {System.Text.Json.JsonSerializer.Serialize(productName)})");
        await WaitUntilAsync("async () => !!(await (await caches.open('mercurius-pages')).match('/sell'))");

        // The connection drops, and the app is opened again from scratch.
        await Context.SetOfflineAsync(true);
        await Page.ReloadAsync();
        await WaitForInteractiveAsync();
        await Assertions.Expect(Page.Locator("#syncIndicator")).ToContainTextAsync("Offline", new LocatorAssertionsToContainTextOptions { Timeout = OfflineTimeoutMs });

        // Sell from the device's copy of the catalog.
        await Page.Locator("#sellSearch").FillAsync(productName);
        await Page.WaitForTimeoutAsync(600);
        await Assertions.Expect(Page.GetByText("Offline: showing this computer's copy of the catalog"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        var tile = Page.Locator(".mud-card").Filter(new LocatorFilterOptions { HasTextString = productName }).First;
        await Assertions.Expect(tile).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        await tile.ClickAsync();
        var charge = Page.Locator("article.content button").Filter(new LocatorFilterOptions { HasTextString = "Charge" }).First;
        await charge.ClickAsync();
        var dialog = Page.Locator(".mud-dialog");
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Charge Card" }).ClickAsync();

        // Kept on this computer, and said so.
        await Assertions.Expect(dialog).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = OfflineTimeoutMs });
        Assert.Contains("Saved on this computer", await WaitForToastAsync());
        await Assertions.Expect(Page.Locator("#syncIndicator")).ToContainTextAsync("1 waiting", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });

        // The connection comes back: the sale goes to the server by itself, and the indicator clears.
        await Context.SetOfflineAsync(false);
        await Assertions.Expect(Page.Locator("#syncIndicator")).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 60000 });

        // It's on the server: the newest invoice is this sale (a card sale, Completed).
        await GotoAppAsync("invoices");
        await Assertions.Expect(FirstRow).ToContainTextAsync("Completed", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task OfflineWithNothingSaved_ShowsAMessage_InsteadOfBreakingTheApp()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("sell");
        await Page.WaitForFunctionAsync("() => navigator.serviceWorker && navigator.serviceWorker.controller !== null",
            null, new PageWaitForFunctionOptions { Timeout = OfflineTimeoutMs });

        await Context.SetOfflineAsync(true);
        // A page whose data this browser never saw: inside the app, a clear message rather than a crash.
        await GotoInAppAsync("audit-trail");
        await Assertions.Expect(Page.Locator("#pageError")).ToContainTextAsync("needs a connection", new LocatorAssertionsToContainTextOptions { Timeout = OfflineTimeoutMs });
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();

        await Context.SetOfflineAsync(false);
    }

    [Fact]
    public async Task ACustomerAddedOffline_WaitsInTheOutbox_AndIsCreatedOnceWhenBackOnline()
    {
        await LoginAsAdminAsync();

        // Online first: the customers page and its list are saved by the service worker.
        await GotoAppAsync("customers");
        await Page.Locator("article.content .mud-table").WaitForAsync(new LocatorWaitForOptions { Timeout = BootTimeoutMs });
        await Page.WaitForFunctionAsync("() => navigator.serviceWorker && navigator.serviceWorker.controller !== null",
            null, new PageWaitForFunctionOptions { Timeout = OfflineTimeoutMs });
        await Page.ReloadAsync();
        await WaitForInteractiveAsync();
        await Page.Locator("article.content .mud-table").WaitForAsync(new LocatorWaitForOptions { Timeout = BootTimeoutMs });
        await WaitUntilAsync("async () => !!(await (await caches.open('mercurius-pages')).match('/customers'))");

        // The connection drops; the app opens again from scratch and a customer is added.
        await Context.SetOfflineAsync(true);
        await Page.ReloadAsync();
        await WaitForInteractiveAsync();
        var firstName = $"Off{Guid.NewGuid():N}"[..10];
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add Customer", Exact = true }).ClickAsync();
        await FillFieldAsync("First name", firstName);
        await FillFieldAsync("Last name", "Offline");
        await Page.Locator(".mud-dialog").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add customer", Exact = true }).ClickAsync();

        // Said so, and kept in the outbox with its label.
        Assert.Contains("saved on this computer", (await WaitForToastAsync(OfflineTimeoutMs)).ToLowerInvariant());
        await Assertions.Expect(Page.Locator("#syncIndicator")).ToContainTextAsync("1 waiting", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await GotoInAppAsync("outbox");
        await Assertions.Expect(Page.Locator("article.content")).ToContainTextAsync("New customer", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });

        // The connection comes back: it's sent by itself, exactly once.
        await Context.SetOfflineAsync(false);
        await Assertions.Expect(Page.Locator("#syncIndicator")).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 60000 });
        var response = await Context.APIRequest.GetAsync($"/api/customers?search={Uri.EscapeDataString(firstName)}");
        Assert.Equal(1, (await response.JsonAsync())!.Value.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task APriceEditedOffline_ThatClashedWithSomeoneElsesEdit_IsHeldForADecision_AndCanBeOverwritten()
    {
        await LoginAsAdminAsync();
        var productName = await CreateTestProductAsync("E2E Price Product");
        var id = int.Parse(System.Text.RegularExpressions.Regex.Match(Page.Url, @"/products/(\d+)").Groups[1].Value);

        // Online: open the edit page so its page and data are saved on this computer.
        await GotoAppAsync($"products/{id}/edit");
        await Assertions.Expect(Page.GetByLabel("Item name")).ToHaveValueAsync(productName, new LocatorAssertionsToHaveValueOptions { Timeout = UiTimeoutMs });
        await Page.WaitForFunctionAsync("() => navigator.serviceWorker && navigator.serviceWorker.controller !== null",
            null, new PageWaitForFunctionOptions { Timeout = OfflineTimeoutMs });
        await Page.ReloadAsync();
        await WaitForInteractiveAsync();
        await Assertions.Expect(Page.GetByLabel("Item name")).ToHaveValueAsync(productName, new LocatorAssertionsToHaveValueOptions { Timeout = UiTimeoutMs });
        await WaitUntilAsync($"async () => !!(await (await caches.open('mercurius-data')).match(location.origin + '/api/products/{id}'))");

        // Offline: the edit page opens again from scratch and the sale price is changed.
        await Context.SetOfflineAsync(true);
        await Page.ReloadAsync();
        await WaitForInteractiveAsync();
        await Assertions.Expect(Page.GetByLabel("Item name")).ToHaveValueAsync(productName, new LocatorAssertionsToHaveValueOptions { Timeout = OfflineTimeoutMs });
        await FillFieldAsync("Sale price", "88.00");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save changes" }).ClickAsync();
        Assert.Contains("saved on this computer", (await WaitForToastAsync(OfflineTimeoutMs)).ToLowerInvariant());

        // Meanwhile a second person edits the same product on the server.
        var other = await Fixture.Browser.Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = BaseUrl, IgnoreHTTPSErrors = true });
        try
        {
            var otherPage = await other.NewPageAsync();
            await otherPage.GotoAsync(LoginPath);
            await otherPage.FillAsync("#email", TestServerFixture.AdminEmail);
            await otherPage.FillAsync("#password", TestServerFixture.AdminPassword);
            await otherPage.ClickAsync("#loginSubmit");
            await otherPage.WaitForURLAsync(url => !url.Contains(LoginPath), new PageWaitForURLOptions { Timeout = 30000 });
            var current = (await (await other.APIRequest.GetAsync($"/api/products/{id}")).JsonAsync())!.Value;
            var edit = await other.APIRequest.PutAsync($"/api/products/{id}", new APIRequestContextOptions
            {
                DataObject = new
                {
                    productCode = current.GetProperty("productCode").GetString(), name = productName, currentCostPrice = 5m, markUpPercentage = 100m,
                    currentSalePrice = 70m, lowStockCount = 0, isActive = true, baseDetailsVersion = current.GetProperty("detailsVersion").GetInt32(),
                    customFieldValues = new Dictionary<int, string?>()
                }
            });
            Assert.Equal(200, (int)edit.Status);
        }
        finally
        {
            await other.CloseAsync();
        }

        // Back online: the queued edit is NOT applied over theirs; it waits for a decision.
        await Context.SetOfflineAsync(false);
        await Assertions.Expect(Page.Locator("#syncIndicator")).ToContainTextAsync("need", new LocatorAssertionsToContainTextOptions { Timeout = 60000 });
        Assert.Equal(70m, (await (await Context.APIRequest.GetAsync($"/api/products/{id}")).JsonAsync())!.Value.GetProperty("currentSalePrice").GetDecimal());

        await GotoInAppAsync("outbox");
        await Assertions.Expect(Page.Locator("article.content")).ToContainTextAsync("Changed elsewhere", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.Locator("article.content")).ToContainTextAsync("Product edit", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });

        // Overwrite with mine: it goes through this time, and the outbox empties.
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Overwrite with mine" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Overwrite", Exact = true }).ClickAsync();
        await Assertions.Expect(Page.Locator("#syncIndicator")).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 60000 });
        Assert.Equal(88m, (await (await Context.APIRequest.GetAsync($"/api/products/{id}")).JsonAsync())!.Value.GetProperty("currentSalePrice").GetDecimal());
    }

    // Polls an async check in the page until it's true. (WaitForFunction takes an async function's
    // Promise as "truthy" straight away, so it can't wait on IndexedDB or the Cache API.)
    private async Task WaitUntilAsync(string asyncCheck, int timeoutMs = OfflineTimeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            if (await Page.EvaluateAsync<bool>(asyncCheck)) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Timed out after {timeoutMs} ms waiting for: {asyncCheck}");
            await Task.Delay(500);
        }
    }

    // A client-side navigation (no page load), the way a click inside the running app moves.
    private async Task GotoInAppAsync(string path)
    {
        await Page.EvaluateAsync("p => Blazor.navigateTo(p)", "/" + path);
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex($"/{path}$"), new PageWaitForURLOptions { Timeout = UiTimeoutMs });
    }
}
