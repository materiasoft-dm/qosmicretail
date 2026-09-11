using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace Mercurius.Mobile.UITests.Infrastructure;

[Collection("Mercurius Mobile UI")]
public abstract class MercuriusMobileTestBase
{
    protected const string AdminEmail = "admin@mercurius.com";
    protected const string AdminPassword = "Admin@123";

    protected AndroidDriver Driver { get; }

    protected MercuriusMobileTestBase(AndroidAppFixture fixture)
    {
        Driver = fixture.Driver;
    }

    // .NET MAUI maps a control's AutomationId to its rendered Android View's resource-id (as
    // "<package>:id/<AutomationId>"), confirmed via `adb shell uiautomator dump` against this
    // build — NOT content-desc, which is the more commonly assumed mapping but comes back empty
    // for every element here. `contains()` sidesteps needing to repeat the package name. Every
    // element this app deliberately tags for testing (see AutomationId="..." across the XAML) is
    // findable this way — no coordinate math, no relying on visible text a future copy change
    // could silently break.
    protected IWebElement ById(string automationId) =>
        Driver.FindElement(By.XPath($"//*[contains(@resource-id, ':id/{automationId}')]"));

    protected IReadOnlyList<IWebElement> AllById(string automationId) =>
        Driver.FindElements(By.XPath($"//*[contains(@resource-id, ':id/{automationId}')]"));

    protected bool ExistsById(string automationId) => AllById(automationId).Count > 0;

    protected IWebElement ByText(string text) =>
        Driver.FindElement(By.XPath($"//*[@text='{text}']"));

    protected bool ExistsByText(string text) =>
        Driver.FindElements(By.XPath($"//*[@text='{text}']")).Count > 0;

    // XPath locators with contains() can't be resolved by UiAutomator2's native selector shortcuts —
    // the driver has to dump the full view hierarchy as XML and evaluate the XPath against it on
    // every call. The local dev catalog this suite runs against now holds this developer's whole
    // real product list (hundreds of rows, ~30 category chips) rather than just the two seeded E2E
    // products, since login unconditionally does a full product sync — see the comment on
    // LoginPage.xaml.cs's post-login sync. That makes each dump noticeably slower and occasionally
    // spiky (GC pauses, emulator scheduling), so 15s was measured to intermittently starve out
    // before a genuinely-present element got polled again. 30s gives enough headroom without
    // masking a real absence (a truly missing element still fails, just slower).
    //
    // On a genuine timeout, the live PageSource + a screenshot are dumped to %TEMP% (named after
    // the automationId) — mirrors Mercurius.E2ETests' server-log capture on failure. Check these
    // first before re-running blind; they're what distinguished the real bugs this suite caught
    // (a missing AutomationId, a stale-view-recycling misfire) from ordinary slowness.
    protected IWebElement WaitForId(string automationId, int timeoutSeconds = 30)
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
            try
            {
                File.WriteAllText(
                    Path.Combine(Path.GetTempPath(), $"mercurius-pagesource-{automationId.Replace(' ', '_')}.xml"),
                    Driver.PageSource);
                File.WriteAllBytes(
                    Path.Combine(Path.GetTempPath(), $"mercurius-screenshot-{automationId.Replace(' ', '_')}.png"),
                    Driver.GetScreenshot().AsByteArray);
            }
            catch { /* best-effort diagnostic */ }
            throw;
        }
    }

    // For native widgets this app doesn't control the AutomationId of — e.g. AlertDialog buttons
    // from DisplayAlertAsync, which expose their label via the standard Android "text" attribute,
    // not "content-desc".
    protected IWebElement WaitForText(string text, int timeoutSeconds = 30)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            return wait.Until(_ =>
            {
                var matches = Driver.FindElements(By.XPath($"//*[@text='{text}']"));
                return matches.Count > 0 ? matches[0] : null;
            })!;
        }
        catch (WebDriverTimeoutException)
        {
            try
            {
                var safe = text.Replace(' ', '_');
                File.WriteAllText(Path.Combine(Path.GetTempPath(), $"mercurius-pagesource-text-{safe}.xml"), Driver.PageSource);
                File.WriteAllBytes(Path.Combine(Path.GetTempPath(), $"mercurius-screenshot-text-{safe}.png"), Driver.GetScreenshot().AsByteArray);
            }
            catch { /* best-effort diagnostic */ }
            throw;
        }
    }

    protected void WaitUntilGone(string automationId, int timeoutSeconds = 30)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        wait.Until(_ => !ExistsById(automationId));
    }

    // Polls an element's text until it reaches the expected value, rather than sleeping a guessed
    // delay. Used around the cart's Increment_/Decrement_ steppers, where CartLine's
    // PropertyChanged-driven cell update needs a real synchronization point before the next rapid
    // tap — a fixed sleep either wastes time or (if too short) taps again before the UI settles.
    protected void WaitForIdText(string automationId, string expectedText, int timeoutSeconds = 30)
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
            try
            {
                var safe = automationId.Replace(' ', '_');
                File.WriteAllText(Path.Combine(Path.GetTempPath(), $"mercurius-pagesource-idtext-{safe}.xml"), Driver.PageSource);
                File.WriteAllBytes(Path.Combine(Path.GetTempPath(), $"mercurius-screenshot-idtext-{safe}.png"), Driver.GetScreenshot().AsByteArray);
            }
            catch { /* best-effort diagnostic */ }
            throw;
        }
    }

    protected void SetText(string automationId, string text)
    {
        // A blind Clear()+SendKeys() is unreliable against MAUI's rendered EditText on Android —
        // without an explicit tap first to establish real input focus, UiAutomator2's SET_TEXT
        // action can silently no-op (the field is left empty, or with only the placeholder
        // showing, even though the call itself doesn't throw). Tapping first, then giving the IME
        // a moment to attach, makes this reliable.
        //
        // Tapping a field's on-screen coordinates only works if nothing else is currently drawn
        // on top of those coordinates — and a keyboard left open by whichever field was focused
        // *before* this one very often covers exactly where the next field sits (e.g. Password
        // sitting right where Email's keyboard was still showing). Hiding any open keyboard first
        // guarantees the tap actually lands on the field instead of on a keyboard key underneath
        // the same screen position.
        if (Driver.IsKeyboardShown())
        {
            Driver.HideKeyboard();
            Thread.Sleep(300);
        }

        var lastError = default(Exception);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                WaitForId(automationId).Click();
                Thread.Sleep(600);
                var element = WaitForId(automationId, timeoutSeconds: 10);
                element.Clear();
                if (!string.IsNullOrEmpty(text))
                {
                    element.SendKeys(text);
                }
                Thread.Sleep(150);
                return;
            }
            catch (Exception ex) when (ex is StaleElementReferenceException or WebDriverTimeoutException)
            {
                lastError = ex;
                Thread.Sleep(500);
            }
        }
        throw new InvalidOperationException($"SetText('{automationId}') failed after 3 attempts.", lastError);
    }

    protected void Tap(string automationId)
    {
        // Same keyboard-occlusion issue as SetText: a keyboard left open by a previously-focused
        // field can visually and spatially cover the target element (e.g. LoginButton sitting right
        // below PasswordEntry), so a coordinate-based click lands on the keyboard instead.
        if (Driver.IsKeyboardShown())
        {
            Driver.HideKeyboard();
            Thread.Sleep(300);
        }
        WaitForId(automationId).Click();
    }

    protected void TapText(string text)
    {
        if (Driver.IsKeyboardShown())
        {
            Driver.HideKeyboard();
            Thread.Sleep(300);
        }
        ByText(text).Click();
    }

    // Logs in fresh (assumes the Login page is showing — e.g. right after AndroidAppFixture's
    // cold-launch force-stop) and waits for the Sell page (the post-login landing page — see
    // AppShell.xaml, "Sell" is the first FlyoutItem) to confirm it actually succeeded.
    protected void LoginAsAdmin()
    {
        SetText("EmailEntry", AdminEmail);
        SetText("PasswordEntry", AdminPassword);
        Tap("LoginButton");
        WaitForId("ProductSearchEntry", timeoutSeconds: 30); // Sell page's search box
    }

    protected void OpenFlyout()
    {
        // The hamburger button Shell generates itself carries no AutomationId we control, but
        // Android's Shell/DrawerLayout renderer gives it the standard accessibility label every
        // Android app's nav-drawer toggle uses.
        if (ExistsByText("Open navigation drawer"))
        {
            TapText("Open navigation drawer");
        }
        else
        {
            Driver.FindElement(By.XPath("//android.widget.ImageButton")).Click();
        }
    }

    protected void NavigateTo(string flyoutTitle)
    {
        OpenFlyout();
        TapText(flyoutTitle);
    }

    // Every test class calls this first rather than assuming a specific prior test already
    // logged in — the driver session (and therefore app state) is shared across the whole
    // collection, so tests must tolerate running in any order, including first.
    protected void EnsureLoggedIn()
    {
        if (ExistsById("ProductSearchEntry")) return; // already on the Sell page

        if (ExistsById("EmailEntry"))
        {
            LoginAsAdmin();
            return;
        }

        // Logged in but sitting on a different page (Receipts/Dashboard/Products).
        NavigateTo("Sell");
        WaitForId("ProductSearchEntry");
    }

    // Leaves the ticket empty — call at the start (and/or end) of any cart-related test so it
    // doesn't inherit state left behind by a previous test in the shared session.
    protected void ClearCart()
    {
        Tap("ClearButton");
    }

    // Opens the flyout, taps "Log Out", then confirms the native "Are you sure?" alert. The
    // alert's accept button is ALSO labeled "Log Out" (see AppShell.xaml.cs's DisplayAlertAsync
    // call), but the dialog's title ("Log Out" in an alertTitle TextView) matches the same
    // by-text XPath and sits earlier in document order, so a plain WaitForText("Log Out").Click()
    // taps the inert title instead of the button and the dialog never closes. Standard Android
    // AlertDialog positive buttons always carry resource-id "android:id/button1" — target that
    // directly instead.
    protected void Logout()
    {
        EnsureLoggedIn();
        NavigateTo("Log Out");
        WaitForId("button1", timeoutSeconds: 10).Click();
        WaitForId("EmailEntry", timeoutSeconds: 15);
    }
}
