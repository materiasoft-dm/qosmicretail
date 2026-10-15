using System.Text.RegularExpressions;
using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests.Blazor;

public class BlazorPurchaseOrderTests : BlazorTestBase
{
    public BlazorPurchaseOrderTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    /// <summary>
    /// The whole pending-order lifecycle: create with a searched-for product, edit it (the classic
    /// Edit page never saved), approve — after which editing is locked — then "Receive" opens the
    /// shipment form with the order and its supplier already chosen.
    /// </summary>
    [Fact]
    public async Task PurchaseOrder_CreateEditApprove_ThenReceive()
    {
        await LoginAsAdminAsync();
        var stamp = Guid.NewGuid().ToString("N")[..8];
        var supplier = $"PO Supplier {stamp}";

        await GotoAppAsync("suppliers");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add Supplier" }).ClickAsync();
        await FillFieldAsync("Supplier name", supplier);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).ClickAsync();
        await Assertions.Expect(Page.Locator(".mud-table-body")).ToContainTextAsync(supplier, new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });

        await CreateProductViaFormAsync($"PO Item A {stamp}", $"POA-{stamp}");
        await CreateProductViaFormAsync($"PO Item B {stamp}", $"POB-{stamp}");

        // Create
        await GotoAppAsync("purchase-orders/new");
        await SelectOptionAsync("Supplier", supplier);
        await PickAutocompleteAsync(Page.GetByLabel("Product 1"), $"POA-{stamp}", $"POA-{stamp}");
        await FillFieldAsync(Page.GetByLabel("Quantity").Nth(0), "10");
        await FillFieldAsync(Page.GetByLabel("Est. unit cost").Nth(0), "2.50");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create purchase order" }).ClickAsync();
        await Page.WaitForURLAsync(new Regex(@"/purchase-orders/\d+$"), new PageWaitForURLOptions { Timeout = UiTimeoutMs });

        var content = Page.Locator("article.content");
        await Assertions.Expect(content).ToContainTextAsync("Pending approval");
        await Assertions.Expect(content).ToContainTextAsync($"PO Item A {stamp}");
        await Assertions.Expect(content).ToContainTextAsync("₱25.00");
        var orderNumber = (await Page.Locator("article.content h4").First.InnerTextAsync()).Trim();
        Assert.StartsWith("PO-", orderNumber);
        var detailsUrl = Page.Url;

        // Edit: add a second line
        await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Edit", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync(new Regex(@"/purchase-orders/\d+/edit$"), new PageWaitForURLOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.GetByLabel("Product 1")).ToHaveValueAsync($"PO Item A {stamp} (POA-{stamp})");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add product" }).ClickAsync();
        await PickAutocompleteAsync(Page.GetByLabel("Product 2"), $"POB-{stamp}", $"POB-{stamp}");
        await FillFieldAsync(Page.GetByLabel("Quantity").Nth(1), "4");
        await FillFieldAsync(Page.GetByLabel("Est. unit cost").Nth(1), "1.25");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save changes" }).ClickAsync();
        await Page.WaitForURLAsync(detailsUrl, new PageWaitForURLOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(content).ToContainTextAsync($"PO Item B {stamp}");
        await Assertions.Expect(content).ToContainTextAsync("₱30.00");

        // Approve — editing is then locked
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Approve" }).ClickAsync();
        await Assertions.Expect(content.Locator(".mud-chip").First).ToHaveTextAsync("Approved", new LocatorAssertionsToHaveTextOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Edit", Exact = true })).ToHaveCountAsync(0);
        await Page.GotoAsync(detailsUrl + "/edit");
        await Assertions.Expect(Page.GetByText("can no longer be edited")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = BootTimeoutMs });

        // Receive: shipment form opens with this order (and so its supplier) selected
        await Page.GotoAsync(detailsUrl);
        await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Receive shipment" }).ClickAsync(new LocatorClickOptions { Timeout = BootTimeoutMs });
        await Page.WaitForURLAsync(new Regex(@"/shipments\?poId=\d+$"), new PageWaitForURLOptions { Timeout = UiTimeoutMs });
        await FillFieldAsync("Tracking number", $"TRK-{stamp}");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).ClickAsync();
        var shipmentRow = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = $"TRK-{stamp}" });
        await Assertions.Expect(shipmentRow).ToContainTextAsync(supplier, new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }
}
