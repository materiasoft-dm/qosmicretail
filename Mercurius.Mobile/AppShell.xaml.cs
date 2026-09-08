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
    }

    private async Task LoadUserLabelAsync()
    {
        var fullName = await _sessionService.GetFullNameAsync();
        var email = await _sessionService.GetEmailAsync();
        FlyoutUserLabel.Text = !string.IsNullOrWhiteSpace(fullName) ? fullName : email;
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
