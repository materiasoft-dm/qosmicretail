using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Support.UI;

namespace Mercurius.Mobile.UITests.Windows.Infrastructure;

// Windows counterpart to Mercurius.Mobile.UITests' MercuriusMobileTestBase. Same shape
// deliberately (ById/WaitForId/Tap/SetText/EnsureLoggedIn/...) so the two suites read the same
// way, but several Android-specific concerns don't apply here:
//   - No on-screen keyboard to occlude fields — Windows has a physical keyboard, so SetText/Tap
//     don't need Android's IsKeyboardShown()/HideKeyboard() dance.
//   - No hamburger/drawer to open — AppShell.xaml sets FlyoutBehavior=Locked for the Desktop
//     idiom, so the sidebar is always visible; NavigateTo just clicks the item directly.
//   - AutomationId maps straight through to WinUI3's own AutomationId (unlike Android, where .NET
//     MAUI maps it to the View's resource-id instead) — no prefix/package-name concatenation
//     needed, just an exact match.
//
// Locator strategy CONFIRMED against a real run (`@AutomationId`/`@Name` XPath, matching
// WinAppDriver's documented standard mapping) — see CLAUDE.md's Windows E2E section for the
// gotchas that real run surfaced (capability-prefixing/Appium.WebDriver version pin, WinAppDriver's
// stdin blocking, ContentDialog title/button ambiguity, SendKeys settle timing).
[Collection("Mercurius Mobile Windows UI")]
public abstract class MercuriusMobileWindowsTestBase
{
    // Falls back to the same credentials Mercurius.Mobile.UITests uses (the dev seed admin) —
    // override via environment variable if ApiConfig.BaseUrl points at a server whose seeded
    // admin password differs (e.g. the live deployment's appsettings.Production.json, which
    // isn't part of this repo). Deliberately not hardcoded as a second literal password: that
    // would mean committing a guess at a real credential to source control.
    protected static readonly string AdminEmail =
        Environment.GetEnvironmentVariable("MERCURIUS_E2E_EMAIL") ?? "admin@mercurius.com";
    protected static readonly string AdminPassword =
        Environment.GetEnvironmentVariable("MERCURIUS_E2E_PASSWORD") ?? "Admin@123";

    private readonly WindowsAppFixture _fixture;

    // Read live off the fixture (not cached at construction) — EnsureSessionAlive() below may
    // have just replaced it with a freshly relaunched session.
    protected WindowsDriver<WindowsElement> Driver => _fixture.Driver;

    protected MercuriusMobileWindowsTestBase(WindowsAppFixture fixture)
    {
        _fixture = fixture;
        // Cheap when healthy, recovers when not — see WindowsAppFixture.EnsureSessionAlive's own
        // comment for why this is needed (a confirmed, external WinUI3 native crash can kill the
        // app mid-suite).
        _fixture.EnsureSessionAlive();
    }

    protected IWebElement ById(string automationId) =>
        Driver.FindElement(By.XPath($"//*[@AutomationId='{automationId}']"));

    protected IReadOnlyList<IWebElement> AllById(string automationId) =>
        Driver.FindElements(By.XPath($"//*[@AutomationId='{automationId}']"));

    protected bool ExistsById(string automationId) => AllById(automationId).Count > 0;

    protected IWebElement ByText(string text) =>
        Driver.FindElement(By.XPath($"//*[@Name='{text}']"));

    protected bool ExistsByText(string text) =>
        Driver.FindElements(By.XPath($"//*[@Name='{text}']")).Count > 0;

    // CONFIRMED (via a real page-source dump, not assumed): a CollectionView DataTemplate's ROOT
    // element loses its own AutomationId on Windows in this MAUI version (10.0.20) — the XAML binds
    // one (e.g. "ProductTile_{0}", "ClinicOption_{0}"), but the rendered automation peer comes back
    // with AutomationId="" regardless. Content NESTED inside that root (a price/stock Label with its
    // own AutomationId, say) keeps its identity fine — only the item's own outer wrapper doesn't
    // forward one. Where a nested id already exists, prefer tapping/asserting on THAT instead (a
    // click anywhere inside the root's bounds still fires the root's own TapGestureRecognizer, so
    // this isn't a workaround so much as just picking a different, working point inside the same
    // clickable area). These two helpers are for the remaining case — a template with no nested id
    // at all (e.g. SettingsPage's clinic rows) — where the only option is positional indexing into
    // the container's direct children. See CLAUDE.md's Windows E2E section for the full writeup.
    protected IReadOnlyList<IWebElement> CollectionItems(string containerAutomationId) =>
        WaitForId(containerAutomationId).FindElements(By.XPath("./*"));

    protected IWebElement CollectionItem(string containerAutomationId, int index)
    {
        var items = CollectionItems(containerAutomationId);
        if (index >= items.Count)
        {
            throw new InvalidOperationException(
                $"CollectionItem('{containerAutomationId}', {index}) — only {items.Count} item(s) present.");
        }
        return items[index];
    }

    // For a ContentDialog's own accept/cancel buttons specifically — NOT a plain by-text match.
    // Confirmed via a real page-source dump: a WinUI ContentDialog puts FOUR elements named e.g.
    // "Log Out" in the tree (the dialog's own top-level window, its title text, the button itself,
    // and — if the same label also opened it, like AppShell's flyout MenuItem — that trigger too),
    // in that document order. A plain WaitForText(text).Click() hits the inert dialog WINDOW first
    // and the dialog never closes. Scoping to LocalizedControlType='button' is unambiguous.
    protected IWebElement WaitForButtonText(string text, int timeoutSeconds = 20)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            return wait.Until(_ =>
            {
                var matches = Driver.FindElements(By.XPath($"//*[@LocalizedControlType='button' and @Name='{text}']"));
                return matches.Count > 0 ? matches[0] : null;
            })!;
        }
        catch (WebDriverTimeoutException)
        {
            DumpDiagnostics($"buttontext-{text}");
            throw;
        }
    }

    protected IWebElement WaitForId(string automationId, int timeoutSeconds = 20)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            return wait.Until(_ =>
            {
                var matches = AllById(automationId);
                return matches.Count > 0 ? matches[0] : null;
            })!;
        }
        catch (WebDriverTimeoutException)
        {
            DumpDiagnostics($"id-{automationId}");
            throw;
        }
    }

    protected IWebElement WaitForText(string text, int timeoutSeconds = 20)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            return wait.Until(_ =>
            {
                var matches = Driver.FindElements(By.XPath($"//*[@Name='{text}']"));
                return matches.Count > 0 ? matches[0] : null;
            })!;
        }
        catch (WebDriverTimeoutException)
        {
            DumpDiagnostics($"text-{text}");
            throw;
        }
    }

    protected void WaitUntilGone(string automationId, int timeoutSeconds = 20)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        wait.Until(_ => !ExistsById(automationId));
    }

    protected void WaitForIdText(string automationId, string expectedText, int timeoutSeconds = 20)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            wait.Until(_ =>
            {
                var matches = AllById(automationId);
                return matches.Count > 0 && matches[0].Text == expectedText;
            });
        }
        catch (WebDriverTimeoutException)
        {
            DumpDiagnostics($"idtext-{automationId}");
            throw;
        }
    }

    private void DumpDiagnostics(string label)
    {
        try
        {
            var safe = label.Replace(' ', '_');
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), $"mercurius-win-pagesource-{safe}.xml"),
                Driver.PageSource);
            File.WriteAllBytes(
                Path.Combine(Path.GetTempPath(), $"mercurius-win-screenshot-{safe}.png"),
                Driver.GetScreenshot().AsByteArray);
        }
        catch
        {
            // Best-effort diagnostic.
        }
    }

    // A real run against WinAppDriver showed this needs the same care Android's SetText documents:
    // a screenshot captured on a downstream timeout showed the target field still genuinely empty
    // (an on-screen blinking caret, no characters) after a plain Click()+Clear()+SendKeys() with no
    // settle delay — a known WinAppDriver flakiness where SendKeys can silently no-op if it arrives
    // before the control has finished processing the focus/click that preceded it. Verifying the
    // element's own Text afterward (rather than trusting SendKeys not throwing) and retrying is what
    // makes this reliable.
    protected void SetText(string automationId, string text)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var element = WaitForId(automationId);
                element.Click();
                Thread.Sleep(300);
                element.Clear();
                if (!string.IsNullOrEmpty(text))
                {
                    element.SendKeys(text);
                }
                Thread.Sleep(200);

                var actual = WaitForId(automationId, timeoutSeconds: 5).Text;
                // A password Entry's Text comes back as masked bullet characters, not the literal
                // plaintext — GetAttribute("IsPassword") looked like the right way to special-case
                // that, but the live element-attribute endpoint doesn't surface it the way the
                // static PageSource dump does (always comes back false), so length-only is the
                // pragmatic fallback whenever the exact match fails. It still catches the failure
                // mode this retry loop exists for (SendKeys silently no-op'd, leaving the field
                // empty) — a same-length-but-wrong-content false positive is an accepted gap.
                var verified = actual == text || actual.Length == text.Length;
                if (verified) return;
                lastError = new InvalidOperationException(
                    $"SetText('{automationId}', '{text}') verification failed — field reads '{actual}'.");
            }
            catch (Exception ex) when (ex is StaleElementReferenceException or WebDriverTimeoutException)
            {
                lastError = ex;
            }
            Thread.Sleep(300);
        }
        throw new InvalidOperationException($"SetText('{automationId}') failed after 3 attempts.", lastError);
    }

    protected void Tap(string automationId) => WaitForId(automationId).Click();

    protected void TapText(string text) => ByText(text).Click();

    // Assumes the Login page is showing (a fresh WindowsAppFixture launch always starts there —
    // no persisted-session complication the way Android's Fast Deployment force-stop has).
    protected void LoginAsAdmin()
    {
        SetText("EmailEntry", AdminEmail);
        SetText("PasswordEntry", AdminPassword);
        Tap("LoginButton");
        WaitForId("ProductSearchEntry", timeoutSeconds: 30); // Sell page's search box

        // The search box itself renders before SalesPage.OnAppearing()'s own async
        // LoadProductsAsync() has necessarily finished populating the grid — confirmed via a real
        // run's diagnostic screenshot showing an empty "No products found" state moments before the
        // tiles actually appeared. Waiting for ProductSearchEntry alone is therefore a race: a test
        // that searches immediately can run before the catalog has loaded. Wait for actual product
        // content too — a nested label's AutomationId (ProductPrice_*), not the tile's own root
        // (ProductTile_*), which never appears at all on Windows for CollectionView items (see
        // CollectionItems' comment) and would make this wait fail every single time, not just when
        // it's genuinely needed. Not coupled to specific fixture data — any product with a price
        // satisfies this.
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(20));
        wait.Until(_ => Driver.FindElements(By.XPath("//*[contains(@AutomationId,'ProductPrice_')]")).Count > 0);
    }

    // No drawer to open — Desktop's FlyoutBehavior is Locked (see AppShell.xaml), so the sidebar
    // is always on screen. Just click the item.
    protected void NavigateTo(string flyoutTitle) => TapText(flyoutTitle);

    protected void EnsureLoggedIn()
    {
        if (ExistsById("ProductSearchEntry")) return; // already on the Sell page

        if (ExistsById("EmailEntry"))
        {
            LoginAsAdmin();
            return;
        }

        NavigateTo("Sell");
        WaitForId("ProductSearchEntry");
    }

    protected void ClearCart() => Tap("ClearButton");

    // The logout confirmation is a native DisplayAlertAsync dialog, same API AppShell.xaml.cs uses
    // on every platform — on Windows this renders as a WinUI ContentDialog. CONFIRMED (via a real
    // page-source dump, not assumed) to have the SAME class of ambiguity Android's AlertDialog has:
    // the dialog's own top-level window is ALSO named "Log Out" (WinUI names a ContentDialog's host
    // window after its Title), and sits before the actual button in document order — so a plain
    // by-text click resolved to that inert window and the dialog never closed. WaitForButtonText
    // scopes to LocalizedControlType='button' specifically to avoid it.
    protected void Logout()
    {
        EnsureLoggedIn();
        NavigateTo("Log Out");
        WaitForButtonText("Log Out", timeoutSeconds: 10).Click();
        WaitForId("EmailEntry", timeoutSeconds: 15);
    }
}
