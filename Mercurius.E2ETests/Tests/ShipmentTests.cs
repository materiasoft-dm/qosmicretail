using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

/// <summary>
/// Covers recording a shipment as received against an approved Purchase Order. Building this
/// test surfaced a fourth real bug (see CLAUDE.md): ShipmentArrivalStatuses was never seeded,
/// and ShipmentArrival.ShipmentArrivalStatus is a required relationship, so creating any shipment
/// failed with a FOREIGN KEY constraint violation — confirmed zero ShipmentArrival rows existed
/// live. Fixed by seeding it in Program.cs alongside InvoiceStatuses.
/// </summary>
public class ShipmentTests : BlazorTestBase
{
    public ShipmentTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    /// <summary>Creates a PO for the given supplier/product and approves it, since the Receive
    /// action is only offered for Approved (or sent) orders. Returns the PO's row on the list.</summary>
    private async Task<ILocator> CreateAndApprovePurchaseOrderAsync(string supplierName, string productName)
    {
        await GotoAppAsync("purchase-orders/new");
        await SelectOptionAsync("Supplier", supplierName);
        await PickAutocompleteAsync(Page.GetByLabel("Product 1"), productName, productName);
        await FillFieldAsync(Page.GetByLabel("Quantity").Nth(0), "25");
        await FillFieldAsync(Page.GetByLabel("Est. unit cost").Nth(0), "4.50");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create purchase order" }).ClickAsync();
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/purchase-orders/\d+$"),
            new PageWaitForURLOptions { Timeout = UiTimeoutMs });

        await GotoAppAsync("purchase-orders");
        var row = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = supplierName });
        await Assertions.Expect(row).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Approve" }).ClickAsync();
        // Once approved, Approve is replaced by "Mark sent" — only after the grid re-renders.
        await Assertions.Expect(row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Mark sent" }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        return row;
    }

    [Fact]
    public async Task ShipmentsList_Loads()
    {
        await LoginAsAdminAsync();
        await GotoAppAsync("shipments");
        await Page.Locator("article.content .mud-table").WaitForAsync(new LocatorWaitForOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }

    [Fact]
    public async Task ReceiveShipment_ForApprovedPurchaseOrder_AppearsInListAsReceived()
    {
        await LoginAsAdminAsync();
        var supplierName = await CreateTestSupplierAsync("E2E Shipment Supplier");
        var productName = await CreateTestProductAsync("E2E Shipment Product");
        var row = await CreateAndApprovePurchaseOrderAsync(supplierName, productName);

        // "Receive" carries the order's id, so the form opens with the order (and so its supplier) chosen.
        await row.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Receive", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/shipments\?poId=\d+$"),
            new PageWaitForURLOptions { Timeout = UiTimeoutMs });

        // MudSelect keeps the bound value in a hidden input, so assert on the select's rendered text:
        // the order is pre-selected, and its supplier came along with it.
        var poSelect = Page.Locator(".mud-select").Filter(new LocatorFilterOptions { Has = Page.Locator("label:has-text('Purchase order')") }).First;
        await Assertions.Expect(poSelect).ToContainTextAsync("PO-", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        var supplierSelect = Page.Locator(".mud-select").Filter(new LocatorFilterOptions { Has = Page.Locator("label:has-text('Supplier')") }).First;
        await Assertions.Expect(supplierSelect).ToContainTextAsync(supplierName,
            new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });

        var trackingNumber = $"TRACK-{Guid.NewGuid():N}"[..16];
        await FillFieldAsync("Tracking number", trackingNumber);
        // Explicitly mark it Received (status id 2) rather than taking the form's Pending default —
        // this test is specifically about a shipment that has arrived.
        await SelectOptionAsync("Status", "Received");
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save", Exact = true }).ClickAsync();

        var shipmentRow = Page.Locator(".mud-table-body tr.mud-table-row").Filter(new LocatorFilterOptions { HasTextString = trackingNumber });
        await Assertions.Expect(shipmentRow).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(shipmentRow).ToContainTextAsync("Received");
        await Assertions.Expect(shipmentRow).ToContainTextAsync(supplierName);
        await AssertNoBlazorErrorAsync();
    }
}
