using System.Diagnostics;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Mercurius.Mobile.UITests.Windows.Infrastructure;

// One shared WinAppDriver server + one shared WindowsDriver session for the whole test run —
// mirrors Mercurius.Mobile.UITests' AndroidAppFixture. Unlike Android (where the launcher-activity
// name is unknown so the fixture attaches to an already-running, manually-launched app), Windows
// gets to do the simpler, more standard thing: point WinAppDriver's "app" capability straight at
// the built .exe and let it launch a genuinely fresh process every run — no stale state, no
// attach-to-existing-instance workaround needed.
public sealed class WindowsAppFixture : IAsyncLifetime
{
    // Mirrors CLAUDE.md's "app must already be built" convention for the Android suite — this
    // fixture launches whatever's already on disk, it does not build it. Build first with:
    //   dotnet build -f net10.0-windows10.0.19041.0 -c Debug   (from Mercurius.Mobile/)
    private static readonly string ExePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", // .../Mercurius.Mobile.UITests.Windows/bin/Debug/net10.0/ -> repo root
        "Mercurius.Mobile", "bin", "Debug", "net10.0-windows10.0.19041.0", "win-x64", "Mercurius.Mobile.exe"));

    private readonly WinAppDriverServerFixture _server = new();

    public WindowsDriver<WindowsElement> Driver { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!File.Exists(ExePath))
        {
            throw new InvalidOperationException(
                $"Mercurius.Mobile.exe not found at {ExePath}. Build it first: " +
                "'dotnet build -f net10.0-windows10.0.19041.0 -c Debug' from Mercurius.Mobile/.");
        }

        KillStaleAppInstances();
        await _server.StartAsync();
        LaunchSession();
    }

    // A leftover instance from a previous run (or a manual dev session — this project got
    // launched/killed a lot by hand while the Sell page UI was being built) would make
    // WinAppDriver attach to stale window state instead of a cold launch. Same reasoning as
    // AndroidAppFixture's force-stop.
    private static void KillStaleAppInstances()
    {
        foreach (var proc in Process.GetProcessesByName("Mercurius.Mobile"))
        {
            try { proc.Kill(); proc.WaitForExit(5000); } catch { /* best-effort */ }
        }
    }

    private void LaunchSession()
    {
        // Deliberately AddAdditionalCapability ("app", not the newer client's vendor-prefixing
        // App/AddAdditionalAppiumOption) — see the csproj comment on the Appium.WebDriver version
        // pin for why WinAppDriver needs the capability unprefixed.
        var options = new AppiumOptions();
        options.AddAdditionalCapability("app", ExePath);
        options.AddAdditionalCapability("newCommandTimeout", 180);

        Driver = new WindowsDriver<WindowsElement>(new Uri($"http://127.0.0.1:{_server.Port}/"), options, TimeSpan.FromSeconds(60));
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2);

        // A real run showed the launched window can come up at a small default size (with the
        // Windows taskbar visible below it) rather than maximized — confirmed via a diagnostic
        // screenshot. SalesPage's Tiles/List toggle footer sits at the bottom of the products
        // panel, so a small window pushes it out of the rendered viewport entirely (not just
        // off-screen-but-scrollable — WaitForId couldn't find it at all). The real product catalog
        // this local dev server syncs is large (see CLAUDE.md's Android E2E section — "this
        // developer's whole real product list"), which makes the page tall enough that window size
        // actually matters here. Maximizing explicitly removes the dependency on whatever size the
        // OS happened to launch it at.
        Driver.Manage().Window.Maximize();
    }

    // Recovers from a genuine, confirmed-external WinUI3 platform instability: a long automated
    // run can trip a native fail-fast crash inside Microsoft.ui.xaml.dll itself (Windows Event Log:
    // Faulting module Microsoft.UI.Xaml.dll, exception 0xc000027b — a known, still-unresolved
    // upstream bug, see CLAUDE.md's Windows E2E section), which kills Mercurius.Mobile.exe outright.
    // Without this, every test after that point fails instantly with "Currently selected window has
    // been closed" instead of running for real. Called at the start of every test (via
    // MercuriusMobileWindowsTestBase's constructor) — cheap when the session is healthy (one no-op
    // property read), and relaunches against the SAME already-running WinAppDriver server when it
    // isn't (WinAppDriver.exe itself doesn't die when the app it's driving does).
    public void EnsureSessionAlive()
    {
        try
        {
            _ = Driver.Title;
            return;
        }
        catch
        {
            // Session or app is dead — fall through to relaunch.
        }

        try { Driver.Quit(); } catch { /* best-effort */ }
        KillStaleAppInstances();
        LaunchSession();
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
}

[CollectionDefinition("Mercurius Mobile Windows UI")]
public class MercuriusMobileWindowsUiCollection : ICollectionFixture<WindowsAppFixture>
{
}
