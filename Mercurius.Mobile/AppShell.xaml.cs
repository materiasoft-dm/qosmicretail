using Mercurius.Mobile.Converters;
using Mercurius.Mobile.Data;
using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class AppShell : Shell
{
    private readonly SessionService _sessionService;
    private readonly LocalDatabase _localDatabase;
    private readonly SyncCoordinator _syncCoordinator;

    public AppShell(SessionService sessionService, LocalDatabase localDatabase, SyncCoordinator syncCoordinator)
    {
        InitializeComponent();
        _sessionService = sessionService;
        _localDatabase = localDatabase;
        _syncCoordinator = syncCoordinator;
        Loaded += async (_, _) => await LoadUserLabelAsync();

        // The shell only exists while someone is signed in — that's exactly when background sync
        // (periodic, on reconnect, on resume) should be running.
        _syncCoordinator.Start();

        // The XAML default locks the sidebar open on Tablet/Desktop idioms (see AppShell.xaml) —
        // deliberate for Windows/Mac Catalyst, where there's no swipe gesture and the sidebar is
        // meant to read as permanent chrome. On Android, override that for every idiom (phone and
        // tablet alike) so the nav is always a collapsible drawer: tap the hamburger to open it,
        // swipe left (or tap outside it) to dismiss — standard Android drawer behavior that Shell's
        // Flyout gives for free, whereas Locked never draws a hamburger or accepts a swipe at all.
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            FlyoutBehavior = FlyoutBehavior.Flyout;
            // Readable rows on the navy sidebar — see the comment on these templates in AppShell.xaml.
            ItemTemplate = (DataTemplate)Resources["AndroidFlyoutItemTemplate"];
            MenuItemTemplate = (DataTemplate)Resources["AndroidMenuItemTemplate"];
        }
    }

    // Android's Back button on a top-level page would finish the activity — i.e. close the till.
    // Beyond being wrong for a POS (one stray press and the cashier is back at the home screen),
    // it also crashed the app: if a text field was focused, MAUI updated its focus state after the
    // window's services were already disposed (ObjectDisposedException in InputView.MapIsFocused,
    // confirmed on the emulator). So: Back closes the sidebar if it's open; on a top-level page it
    // sends the app to the background instead, exactly like pressing Home.
    protected override bool OnBackButtonPressed()
    {
        if (FlyoutBehavior == FlyoutBehavior.Flyout && FlyoutIsPresented)
        {
            FlyoutIsPresented = false;
            return true;
        }

#if ANDROID
        if (Navigation.NavigationStack.Count <= 1 && Navigation.ModalStack.Count == 0)
        {
            Platform.CurrentActivity?.MoveTaskToBack(true);
            return true;
        }
#endif
        return base.OnBackButtonPressed();
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

        // Last chance to upload anything that exists only on this device. Logging out used to
        // wipe the local database outright — an offline logout silently destroyed real sales.
        await _syncCoordinator.SyncNowAsync();
        var (pendingSales, pendingProducts) = await _localDatabase.GetPendingUploadCountsAsync();
        if (pendingSales + pendingProducts > 0)
        {
            var what = string.Join(" and ", new[]
            {
                pendingSales > 0 ? $"{pendingSales} sale{(pendingSales == 1 ? "" : "s")}" : null,
                pendingProducts > 0 ? $"{pendingProducts} new/edited product{(pendingProducts == 1 ? "" : "s")}" : null
            }.Where(s => s != null));

            var logOutAnyway = await DisplayAlertAsync(
                "Not everything has uploaded",
                $"{what} {(pendingSales + pendingProducts == 1 ? "hasn't" : "haven't")} reached the server yet (no connection, or the server is unreachable).\n\n" +
                "If you log out now, they stay safely on this device and upload automatically the next time anyone signs in here — still credited to the cashier who made them.",
                "Log Out Anyway", "Stay Signed In");
            if (!logOutAnyway) return;
        }

        _syncCoordinator.Stop();

        // Must run while the token is still stored — it's what identifies who signed out.
        await IPlatformApplication.Current!.Services.GetRequiredService<AuthApiService>().NotifyLogoutAsync();

        // Clear the cached catalog too, so a different user logging in on this device next
        // doesn't briefly see the previous user's data before the first sync completes.
        await _localDatabase.ClearAllAsync();
        _sessionService.ClearSession();

        var loginPage = IPlatformApplication.Current!.Services.GetRequiredService<LoginPage>();
        Application.Current!.Windows[0].Page = loginPage;
    }
}
