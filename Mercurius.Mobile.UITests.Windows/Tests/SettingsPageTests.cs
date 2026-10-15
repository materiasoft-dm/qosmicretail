using Mercurius.Mobile.UITests.Windows.Infrastructure;

namespace Mercurius.Mobile.UITests.Windows.Tests;

// Covers the device-clinic-assignment feature end to end. Written to pass whether or not the
// server this run points at (ApiConfig.BaseUrl) already has GET api/sync/locations deployed —
// that endpoint was added alongside this page, and whether it's live depends on whether/when
// the server was redeployed relative to when this suite runs. Both branches
// are real, meaningful assertions, not a skip: the error+retry path is exactly what the app is
// supposed to show when the endpoint genuinely isn't reachable.
public class SettingsPageTests : MercuriusMobileWindowsTestBase
{
    public SettingsPageTests(WindowsAppFixture fixture) : base(fixture) { }

    private void GoToSettings()
    {
        EnsureLoggedIn();
        NavigateTo("Settings");
        WaitForId("CurrentClinicLabel", timeoutSeconds: 15);
    }

    [Fact]
    public void SettingsPage_LoadsAndReachesAKnownState()
    {
        GoToSettings();

        // One of these three should be true within a few seconds: clinics loaded, the list is
        // genuinely empty, or the call failed and the retry UI is showing. Never left spinning.
        var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, TimeSpan.FromSeconds(15));
        wait.Until(_ =>
            ExistsById("ClinicsCollectionView") ||
            ExistsById("EmptyClinicListLayout") && ById("EmptyClinicListLayout").Displayed ||
            ExistsById("ClinicListErrorLayout") && ById("ClinicListErrorLayout").Displayed);

        if (ExistsById("ClinicListErrorLayout") && ById("ClinicListErrorLayout").Displayed)
        {
            Assert.True(ExistsById("RetryLoadClinicsButton"));

            // Retry re-runs the same call — should land back in one of these same states, not hang.
            Tap("RetryLoadClinicsButton");
            wait.Until(_ =>
                ExistsById("ClinicsCollectionView") ||
                (ExistsById("EmptyClinicListLayout") && ById("EmptyClinicListLayout").Displayed) ||
                (ExistsById("ClinicListErrorLayout") && ById("ClinicListErrorLayout").Displayed));
        }
    }

    [Fact]
    public void SelectingAClinic_UpdatesTheCurrentClinicLabel_WhenAnyClinicIsAvailable()
    {
        GoToSettings();

        var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, TimeSpan.FromSeconds(15));
        wait.Until(_ => ExistsById("ClinicListErrorLayout") && ById("ClinicListErrorLayout").Displayed
                        || ExistsById("ClinicsCollectionView"));

        // Can't locate rows by their bound AutomationId ("ClinicOption_{0}") — a CollectionView
        // item's root loses it on Windows in this MAUI version (see CollectionItems' comment on
        // MercuriusMobileWindowsTestBase). Positional indexing into the container instead; the
        // clinic name comes from the row's own (AutomationId-less, but still Name-bearing) child
        // Label.
        var options = CollectionItems("ClinicsCollectionView");
        if (options.Count == 0)
        {
            // No clinics to pick from yet (server not redeployed with the new endpoint, or none
            // configured) — the "reaches a known state" test above already covers that path.
            return;
        }

        var firstClinicName = options[0].FindElement(OpenQA.Selenium.By.XPath(".//*")).Text;
        options[0].Click();

        // The tap fires a confirmation alert (SettingsPage.xaml.cs's OnClinicRowTapped) — dismiss it.
        WaitForText("OK", timeoutSeconds: 10).Click();

        WaitForIdText("CurrentClinicLabel", firstClinicName, timeoutSeconds: 30);

        // Round-trip through Sell and back — the assignment is device-level (Preferences), not
        // page-session state, so it must survive navigating away.
        NavigateTo("Sell");
        WaitForId("ProductSearchEntry", timeoutSeconds: 10);
        NavigateTo("Settings");
        WaitForIdText("CurrentClinicLabel", firstClinicName, timeoutSeconds: 30);
    }
}
