using Mercurius.Mobile.UITests.Infrastructure;

namespace Mercurius.Mobile.UITests.Tests;

public class LoginTests : MercuriusMobileTestBase
{
    public LoginTests(AndroidAppFixture fixture) : base(fixture) { }

    [Fact]
    public void Login_WithValidCredentials_LandsOnSellPage()
    {
        EnsureLoggedIn();

        // "Sell" is the first Shell flyout item (see AppShell.xaml) — landing there confirms both
        // that login succeeded and that the app opens straight to the register, not the Dashboard.
        Assert.True(ExistsById("ProductSearchEntry"));
        Assert.True(ExistsById("ChargeButton"));
    }

    [Fact]
    public void Login_WithInvalidPassword_ShowsError()
    {
        Logout(); // guarantees we're on the Login page regardless of what ran before this

        SetText("EmailEntry", AdminEmail);
        SetText("PasswordEntry", "definitely-wrong-password");
        Tap("LoginButton");

        var error = WaitForId("LoginErrorLabel", timeoutSeconds: 30);
        Assert.Contains("Invalid", error.Text, StringComparison.OrdinalIgnoreCase);

        // Leave the app logged in for the rest of the suite.
        SetText("PasswordEntry", AdminPassword);
        Tap("LoginButton");
        WaitForId("ProductSearchEntry", timeoutSeconds: 30);
    }
}
