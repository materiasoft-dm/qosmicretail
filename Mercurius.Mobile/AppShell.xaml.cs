using Mercurius.Mobile.Converters;
using Mercurius.Mobile.Data;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class AppShell : Shell
{
    private readonly SessionService _sessionService;
    private readonly LocalDatabase _localDatabase;

    public AppShell(SessionService sessionService, LocalDatabase localDatabase)
    {
        InitializeComponent();
        _sessionService = sessionService;
        _localDatabase = localDatabase;
        Loaded += async (_, _) => await LoadUserLabelAsync();

        // The XAML default locks the sidebar open on Tablet/Desktop idioms (see AppShell.xaml) —
        // deliberate for Windows/Mac Catalyst, where there's no swipe gesture and the sidebar is
        // meant to read as permanent chrome. On Android, override that for every idiom (phone and
        // tablet alike) so the nav is always a collapsible drawer: tap the hamburger to open it,
        // swipe left (or tap outside it) to dismiss — standard Android drawer behavior that Shell's
        // Flyout gives for free, whereas Locked never draws a hamburger or accepts a swipe at all.
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            FlyoutBehavior = FlyoutBehavior.Flyout;
        }
    }

    private static readonly InitialsConverter InitialsConverter = new();

    private async Task LoadUserLabelAsync()
    {
        var fullName = await _sessionService.GetFullNameAsync();
        var email = await _sessionService.GetEmailAsync();
        var displayName = !string.IsNullOrWhiteSpace(fullName) ? fullName : email;
        FlyoutUserLabel.Text = displayName;
        FlyoutUserInitials.Text = (string)InitialsConverter.Convert(displayName, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync("Log Out", "Are you sure you want to log out?", "Log Out", "Cancel");
        if (!confirmed) return;

        // Clear the cached catalog too, so a different user logging in on this device next
        // doesn't briefly see the previous user's data before the first sync completes.
        await _localDatabase.ClearAllAsync();
        _sessionService.ClearSession();

        var loginPage = IPlatformApplication.Current!.Services.GetRequiredService<LoginPage>();
        Application.Current!.Windows[0].Page = loginPage;
    }
}
