using Microsoft.Playwright;
using Xunit;

namespace Mercurius.E2ETests.Infrastructure;

/// <summary>Shared setup helpers for tests that need a product/supplier to already exist before
/// exercising the actual feature under test.</summary>
public static class TestDataHelpers
{
    /// <summary>Creates a product via the Products page's ajax-form create modal. Returns the
    /// product's display name.</summary>
    public static async Task<string> CreateTestProductAsync(this IPage page, string namePrefix = "E2E Product")
    {
        var uniqueCode = $"E2E-{Guid.NewGuid():N}".Substring(0, 14);
        var uniqueName = $"{namePrefix} {Guid.NewGuid():N}".Substring(0, Math.Min(50, namePrefix.Length + 33));

        await page.GotoAsync("/Products");
        await page.ClickAsync("button[data-bs-target='#createProductModal']");
        await page.Locator("#createProductModal.show").WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await page.FillAsync("#Create_Name", uniqueName);
        await page.FillAsync("#Create_ProductCode", uniqueCode);
        await page.FillAsync("#Create_CurrentCostPrice", "5.00");
        await page.FillAsync("#Create_CurrentSalePrice", "9.99");
        // LowStockCount and MarkUpPercentage are non-nullable decimals — an empty input submits
        // "" and fails model binding outright ("The value '' is invalid"), not just validation.
        await page.FillAsync("#Create_LowStockCount", "5");
        await page.FillAsync("#Create_MarkUpPercentage", "0");

        // The create form is an ajax-form: the click fires a fetch, and only once its .then()
        // callback runs does the JS do `window.location.href = redirect`. That redirect target is
        // literally /Products again (Url.Action(nameof(Index))) — the same URL we're already on —
        // so neither a "URL contains X" check nor a "URL changed" check can ever detect the actual
        // navigation. Wait instead for the modal to become hidden: a real page reload always
        // starts with it closed, regardless of what the resulting URL string looks like.
        await page.ClickAsync("#createProductModal button:has-text('Save Product')");
        await page.Locator("#createProductModal.show").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = 10000 });

        return uniqueName;
    }

    /// <summary>Creates a supplier via the plain (non-ajax) Suppliers/Create form. Returns the
    /// supplier's name.</summary>
    public static async Task<string> CreateTestSupplierAsync(this IPage page, string namePrefix = "E2E Supplier")
    {
        var name = $"{namePrefix} {Guid.NewGuid():N}".Substring(0, Math.Min(50, namePrefix.Length + 33));
        await page.GotoAsync("/Suppliers/Create");
        await page.FillAsync("#Name", name);
        var urlBefore = page.Url;
        await page.ClickAsync("input[type=submit][value=Create]");
        await page.WaitForURLAsync(url => url.ToString() != urlBefore, new PageWaitForURLOptions { Timeout = 10000 });
        return name;
    }
}
