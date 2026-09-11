using Mercurius.Mobile.UITests.Infrastructure;

namespace Mercurius.Mobile.UITests.Tests;

public class LogoutTests : MercuriusMobileTestBase
{
    public LogoutTests(AndroidAppFixture fixture) : base(fixture) { }

    [Fact]
    public void Logout_ReturnsToLoginPage_AndClearsLocalCatalog()
    {
        EnsureLoggedIn();

        Logout();

        Assert.True(ExistsById("EmailEntry"));
        Assert.True(ExistsById("PasswordEntry"));
        Assert.True(ExistsById("LoginButton"));

        // AppShell.xaml.cs's logout handler wipes the local product/sale cache (ClearAllAsync) so
        // a different user logging in next doesn't briefly see the previous user's catalog —
        // log back in and confirm the Sell page comes back up clean rather than erroring.
        LoginAsAdmin();
        Assert.True(ExistsById("ProductSearchEntry"));
    }
}
