using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace Mercurius.Mobile.UITests.Infrastructure;

// One shared Appium server + one shared AndroidDriver session for the whole test run — mirrors
// Mercurius.E2ETests' MercuriusCollectionFixture (one server, one browser, shared across the
// collection). The app is expected to already be built and installed on the target
// emulator/device (see CLAUDE.md's "dotnet build -f net10.0-android -t:Run" — this fixture does
// not build or deploy it, only launches and drives whatever's already installed), matching how
// the web E2E fixture execs an already-built Mercurius.dll rather than rebuilding it itself.
public sealed class AndroidAppFixture : IAsyncLifetime
{
    private const string PackageName = "com.companyname.mercurius.mobile";
    private static readonly string AdbPath = @"C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe";

    private readonly AppiumServerFixture _server = new();

    public AndroidDriver Driver { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Force-stop first so every test run starts from a cold app launch (no leftover
        // in-memory state — e.g. a cart from a previous run, or a stuck modal), then launch it
        // via the same `monkey` launcher-intent trick used manually earlier in this project's
        // history, since it doesn't require knowing .NET MAUI's generated launcher-activity name.
        //
        // Deliberately NOT `pm clear`: this Debug build is deployed via .NET for Android's Fast
        // Deployment (assemblies pushed to the app's private
        // files/.__override__/<abi> directory by `dotnet build -t:Run` rather than embedded in the
        // APK), and `pm clear` wipes that directory along with everything else. The app then
        // fatally aborts on next launch with "No assemblies found ... Assuming this is part of Fast
        // Deployment. Exiting..." — confirmed via logcat. A force-stop leaves that directory intact
        // and leaves the persisted login session/local product cache intact too, which has one real
        // consequence worth knowing: EnsureLoggedIn() sees the existing session and skips the Login
        // page, so LoginPage.xaml.cs's post-login product sync never re-runs on a plain relaunch. If
        // you reset test data server-side (e.g. via the Admin Data Query API) between runs, log out
        // and back in through the app afterward (or reinstall) so that sync actually picks it up —
        // otherwise the device's local cache stays stale until some other explicit sync trigger.
        RunAdb($"shell am force-stop {PackageName}");
        await Task.Delay(500);
        RunAdb($"shell monkey -p {PackageName} -c android.intent.category.LAUNCHER 1");
        await Task.Delay(2000);

        await _server.StartAsync();

        var options = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = "UiAutomator2"
        };
        // Attach to the already-launched app rather than asking Appium to (re)launch it —
        // sidesteps ever needing to know the generated launcher-activity class name.
        options.AddAdditionalAppiumOption("appium:autoLaunch", false);
        options.AddAdditionalAppiumOption("appium:newCommandTimeout", 180);
        options.AddAdditionalAppiumOption("appium:noReset", true);

        Driver = new AndroidDriver(new Uri($"http://127.0.0.1:{_server.Port}/"), options, TimeSpan.FromSeconds(60));
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);

        await EnsureFreshSessionAsync();
    }

    // A force-stop/relaunch (see above) preserves the persisted login session, so a suite run that
    // starts while an earlier run's session is still active lands straight on the Sell page and
    // skips LoginPage.xaml.cs's post-login product sync entirely. That bit twice while iterating on
    // this suite: server-side test data reset via the Admin Data Query API between runs (e.g.
    // putting E2E-ALPHA-001's stock back to 50) never reached the device, so a single correct sale
    // read as a mysterious 2-unit stock drop — the local cache was already stale by one unit before
    // the sale even happened. Logging out here (not just force-stopping) guarantees every suite run
    // starts from the Login page, so the very first test's EnsureLoggedIn() always does a real
    // login and therefore a real sync.
    private async Task EnsureFreshSessionAsync()
    {
        bool ExistsById(string automationId) =>
            Driver.FindElements(By.XPath($"//*[contains(@resource-id, ':id/{automationId}')]")).Count > 0;

        if (!ExistsById("ProductSearchEntry")) return; // already on the Login page — nothing to do

        if (Driver.FindElements(By.XPath("//*[@text='Open navigation drawer']")).Count > 0)
        {
            Driver.FindElement(By.XPath("//*[@text='Open navigation drawer']")).Click();
        }
        else
        {
            Driver.FindElement(By.XPath("//android.widget.ImageButton")).Click();
        }
        await Task.Delay(300);

        Driver.FindElement(By.XPath("//*[@text='Log Out']")).Click();
        await Task.Delay(500);

        // Standard Android AlertDialog positive button — see MercuriusMobileTestBase.Logout for why
        // this can't just be another by-text("Log Out") lookup (the dialog's title says "Log Out" too).
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
        wait.Until(_ => Driver.FindElements(By.XPath("//*[contains(@resource-id, ':id/button1')]")).Count > 0);
        Driver.FindElement(By.XPath("//*[contains(@resource-id, ':id/button1')]")).Click();

        wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(15));
        wait.Until(_ => ExistsById("EmailEntry"));
    }

    public async Task DisposeAsync()
    {
        try
        {
            Driver?.Quit();
        }
        catch
        {
            // Best-effort.
        }
        await _server.DisposeAsync();
    }

    private static void RunAdb(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = AdbPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        });
        process?.WaitForExit(15000);
    }
}

[CollectionDefinition("Mercurius Mobile UI")]
public class MercuriusMobileUiCollection : ICollectionFixture<AndroidAppFixture>
{
}
