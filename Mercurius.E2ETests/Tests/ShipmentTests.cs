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
public class ShipmentTests : MercuriusTestBase
{
    public ShipmentTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    /// <summary>Creates a PO for the given supplier/product and approves it, since Shipment/Create's
    /// PO dropdown only lists Approved or OrderSent orders. Returns the PO's row locator on the
    /// PurchaseOrders index (identified by supplier name, which we control and is unique).</summary>
    private async Task CreateAndApprovePurchaseOrderAsync(string supplierName, string productName)
    {
        await Page.GotoAsync("/PurchaseOrders/Create");
        await Page.SelectOptionAsync("select[name=SupplierId]", new SelectOptionValue { Label = supplierName });

        var productOptionValue = await Page.Locator($"select[name=productIds] option:has-text('{productName}')").GetAttributeAsync("value");
        Assert.False(string.IsNullOrEmpty(productOptionValue), $"Could not find a product option containing '{productName}' in the dropdown.");
        await Page.SelectOptionAsync("select[name=productIds]", productOptionValue!);
        await Page.FillAsync("input[name=quantities]", "25");
        await Page.FillAsync("input[name=costs]", "4.50");

        await Page.ClickAsync("button:has-text('Save Purchase Order')");
        await Page.WaitForURLAsync(url => url.Contains("/PurchaseOrders") && !url.Contains("Create"), new PageWaitForURLOptions { Timeout = 10000 });

        // Approve it — a plain form POST that 302-redirects back to /PurchaseOrders. Waiting for
        // the POST's own Response resolves as soon as that redirect response arrives, which can
        // be a beat before the browser finishes following it and the page reloads — the same
        // class of race as the ajax-form case, just via a server redirect instead of client JS.
        // Wait for the row's own re-render instead: once approved, its "Approve" button is
        // replaced by "Mark Sent", which only exists after the page has actually reloaded.
        var row = Page.Locator($"#poTable tbody tr:has-text('{supplierName}')");
        await row.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await row.Locator("button[title=Approve]").ClickAsync();
        row = Page.Locator($"#poTable tbody tr:has-text('{supplierName}')");
        await row.Locator("button[title='Mark Sent']").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task ShipmentsList_Loads()
    {
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Shipment");
        await Page.Locator("#shipmentsTable").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task ReceiveShipment_ForApprovedPurchaseOrder_AppearsInListAsReceived()
    {
        await LoginAsAdminAsync();
        var supplierName = await Page.CreateTestSupplierAsync("E2E Shipment Supplier");
        var productName = await Page.CreateTestProductAsync("E2E Shipment Product");
        await CreateAndApprovePurchaseOrderAsync(supplierName, productName);

        // Grab the just-approved PO's id off its "Receive" link so we can go straight to its
        // shipment form, the same way a user clicks "Receive" from the Purchase Orders list.
        var poRow = Page.Locator($"#poTable tbody tr:has-text('{supplierName}')");
        var receiveHref = await poRow.Locator("a:has-text('Receive')").GetAttributeAsync("href");
        Assert.False(string.IsNullOrEmpty(receiveHref), "Could not find a Receive link on the approved PO's row.");

        await Page.GotoAsync(receiveHref!);

        // The PO should be pre-selected (via the poId query string) and, once selected, the
        // supplier auto-fills from an AJAX call — wait for that to actually resolve rather than
        // asserting immediately.
        await Assertions.Expect(Page.Locator("#supplier-name")).ToHaveValueAsync(supplierName, new LocatorAssertionsToHaveValueOptions { Timeout = 10000 });

        var trackingNumber = $"TRACK-{Guid.NewGuid():N}".Substring(0, 16);
        await Page.FillAsync("input[name=ShipmentArrivalDate]", DateTime.UtcNow.ToString("yyyy-MM-dd"));
        await Page.FillAsync("input[name=TrackingNumber]", trackingNumber);
        // Explicitly mark it Received (seeded as status id 2) rather than leaving the form's
        // default of 1 (Pending) — this test is specifically about a shipment that has arrived.
        await Page.FillAsync("input[name=ShipmentArrivalStatusId]", "2");

        await Page.ClickAsync("button:has-text('Save Shipment')");
        await Page.WaitForURLAsync(url => url.Contains("/Shipment") && !url.Contains("Create"), new PageWaitForURLOptions { Timeout = 10000 });

        await Page.Locator("#searchInput").PressSequentiallyAsync(trackingNumber, new LocatorPressSequentiallyOptions { Delay = 20 });
        var shipmentRow = Page.Locator($"#shipmentsTable tbody tr:has-text('{trackingNumber}')");
        await shipmentRow.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await Assertions.Expect(shipmentRow).ToContainTextAsync("Received");
    }
}
