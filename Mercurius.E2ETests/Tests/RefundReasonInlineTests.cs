using Mercurius.E2ETests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Tests;

/// <summary>
/// The Refund dialog lets a user who may manage refund reasons add one on the spot (instead of
/// leaving the refund to go to Settings first). Adding a reason needs the server's new id, so it is
/// online-only; the refund itself still queues offline with an existing reason.
/// </summary>
public class RefundReasonInlineTests : BlazorTestBase
{
    public RefundReasonInlineTests(MercuriusCollectionFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RefundDialog_AdminCanAddAReasonInline_AndRefundWithIt()
    {
        await LoginAsAdminAsync();
        var productName = await CreateTestProductAsync("E2E Refund Product");

        // Ring up one sale so there is something to refund.
        await GotoAppAsync("sell");
        await Page.Locator("#sellSearch").FillAsync(productName);
        await Page.WaitForTimeoutAsync(600);
        var tile = Page.Locator(".mud-card").Filter(new LocatorFilterOptions { HasTextString = productName }).First;
        await Assertions.Expect(tile).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        await tile.ClickAsync();
        var charge = Page.Locator("article.content button").Filter(new LocatorFilterOptions { HasTextString = "Charge" }).First;
        await Assertions.Expect(charge).ToBeEnabledAsync(new LocatorAssertionsToBeEnabledOptions { Timeout = UiTimeoutMs });
        await charge.ClickAsync();
        var dialog = Page.Locator(".mud-dialog");
        await Assertions.Expect(dialog).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Charge Card" }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });

        // Open the newest invoice and start a refund.
        await GotoAppAsync("invoices");
        await Assertions.Expect(FirstRow).ToContainTextAsync("Completed", new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await FirstRow.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "View" }).ClickAsync();
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/invoices/\d+$"), new PageWaitForURLOptions { Timeout = UiTimeoutMs });
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Refund", Exact = true }).First
            .ClickAsync(new LocatorClickOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(dialog).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = UiTimeoutMs });

        // No reasons yet in a fresh database shows the field straight away; otherwise it sits behind "New reason".
        var reasonName = $"E2E Reason {Guid.NewGuid():N}"[..20];
        var showAdd = dialog.GetByTestId("show-add-reason");
        if (await showAdd.CountAsync() > 0) await showAdd.ClickAsync();
        await dialog.Locator("[data-testid='new-reason'] input, input[data-testid='new-reason']").First.FillAsync(reasonName);
        await dialog.GetByTestId("add-reason").ClickAsync();

        // The new reason is added to the list and already selected.
        // The add field closes once the reason exists; the refund below only succeeds if it was selected too
        // (a refund without a reason is refused).
        await Assertions.Expect(dialog.Locator("[data-testid='new-reason']")).ToHaveCountAsync(0,
            new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });

        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Refund", Exact = true }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = UiTimeoutMs });
        await Assertions.Expect(Page.Locator("article.content")).ToContainTextAsync(reasonName,
            new LocatorAssertionsToContainTextOptions { Timeout = UiTimeoutMs });
        await AssertNoBlazorErrorAsync();
    }
}
