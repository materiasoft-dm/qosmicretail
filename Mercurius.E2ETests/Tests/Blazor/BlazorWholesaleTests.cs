using System.Text.RegularExpressions;
using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests.Blazor;

public class BlazorWholesaleTests : BlazorTestBase
{
    public BlazorWholesaleTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    /// <summary>
    /// A wholesale order end to end: lines are priced at the product's wholesale price (or retail
    /// when it has none), a part payment leaves it Partially paid, recording the balance makes it
    /// Paid, and the order's stock comes off the product just like a POS sale.
    /// </summary>
    [Fact]
    public async Task WholesaleOrder_PricedAtWholesale_PartPaidThenSettled_DeductsStock()
    {
        await LoginAsAdminAsync();
        var stamp = Guid.NewGuid().ToString("N")[..8];

        // Retail 10.00 (cost 5 + 100%) with a 7.50 wholesale price, and one with no wholesale price.
        await CreateProductViaFormAsync($"WS Item A {stamp}", $"WSA-{stamp}");
        var productAUrl = Page.Url;
        await Page.GotoAsync(productAUrl + "/edit");
        await WaitForInteractiveAsync();
        await FillFieldAsync("Wholesale price", "7.50");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save changes" }).ClickAsync();
        await Page.WaitForURLAsync(productAUrl, new PageWaitForURLOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.Locator("article.content")).ToContainTextAsync("₱7.50");
        await CreateProductViaFormAsync($"WS Item B {stamp}", $"WSB-{stamp}");

        var customerFirstName = $"WS{stamp}";
        await GotoAppAsync("customers");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add Customer", Exact = true }).ClickAsync();
        await FillFieldAsync("First name", customerFirstName);
        await FillFieldAsync("Last name", "Distributor");
        await Page.Locator(".mud-dialog").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add customer", Exact = true }).ClickAsync();
        Assert.Contains("Customer added.", await WaitForToastAsync());

        // 10 × 7.50 + 2 × 10.00 (retail fallback) = 95.00, 50.00 paid now.
        await GotoAppAsync("wholesale/new");
        await PickAutocompleteAsync(Page.GetByLabel("Customer"), customerFirstName, customerFirstName);
        await PickAutocompleteAsync(Page.GetByLabel("Product 1"), $"WSA-{stamp}", $"WSA-{stamp}");
        await FillFieldAsync(Page.GetByLabel("Quantity").Nth(0), "10");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add product" }).ClickAsync();
        await PickAutocompleteAsync(Page.GetByLabel("Product 2"), $"WSB-{stamp}", $"WSB-{stamp}");
        await FillFieldAsync(Page.GetByLabel("Quantity").Nth(1), "2");
        var form = Page.Locator("article.content");
        await Assertions.Expect(form).ToContainTextAsync("No wholesale price — retail");
        await Assertions.Expect(form).ToContainTextAsync("₱95.00");
        await FillFieldAsync("Amount paid now", "50");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create wholesale order" }).ClickAsync();
        await Page.WaitForURLAsync(new Regex(@"/wholesale/\d+$"), new PageWaitForURLOptions { Timeout = UiTimeoutMs });

        var content = Page.Locator("article.content");
        var orderNumber = (await content.Locator("h4").First.InnerTextAsync()).Trim();
        Assert.StartsWith("WS-", orderNumber);
        await Assertions.Expect(content.Locator(".mud-chip").First).ToHaveTextAsync("Partially paid");
        await Assertions.Expect(content).ToContainTextAsync("₱75.00");
        await Assertions.Expect(content).ToContainTextAsync("₱45.00");

        // Settle the balance.
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Full balance" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Record payment", Exact = true }).ClickAsync();
        await Assertions.Expect(content.Locator(".mud-chip").First).ToHaveTextAsync("Paid", new LocatorAssertionsToHaveTextOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Record payment", Exact = true })).ToHaveCountAsync(0);

        // Listed on the Wholesale page.
        await GotoAppAsync("wholesale");
        await SearchAsync("Search by order # or customer...", orderNumber);
        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = orderNumber });
        await Assertions.Expect(row).ToContainTextAsync($"{customerFirstName} Distributor", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(row).ToContainTextAsync("Paid");

        // New products start at zero stock, so selling 10 leaves -10.
        await GotoAppAsync(new Uri(productAUrl).AbsolutePath);
        await Assertions.Expect(Page.Locator("article.content")).ToContainTextAsync("-10", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }
}
