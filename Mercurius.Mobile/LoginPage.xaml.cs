using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class LoginPage : ContentPage
{
    private readonly AuthApiService _authApiService;
    private readonly SessionService _sessionService;
    private readonly SyncService _syncService;

    public LoginPage(AuthApiService authApiService, SessionService sessionService, SyncService syncService)
    {
        InitializeComponent();
        _authApiService = authApiService;
        _sessionService = sessionService;
        _syncService = syncService;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        ErrorLabel.IsVisible = false;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("Please enter both email and password.");
            return;
        }

        SetBusy(true);
        var result = await _authApiService.LoginAsync(email, password);
        SetBusy(false);

        if (!result.Success || result.Login == null)
        {
            ShowError(result.ErrorMessage ?? "Login failed.");
            return;
        }

        await _sessionService.SaveSessionAsync(result.Login);

        // A successful login already proves connectivity, so this is the one guaranteed moment to
        // refresh the local product catalog before landing on the Sell page — without it, a device
        // that just logged in (or logged out/back in, which wipes the local cache via
        // AppShell.xaml.cs's ClearAllAsync) lands on an empty Sell page with no obvious way to know
        // why. Best-effort: a sync failure here shouldn't block login on an otherwise offline-first app.
        SetBusy(true);
        try
        {
            await _syncService.SyncProductsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Post-login product sync failed: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }

        var shell = IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
        Application.Current!.Windows[0].Page = shell;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void SetBusy(bool busy)
    {
        LoginButton.IsEnabled = !busy;
        LoadingIndicator.IsVisible = busy;
        LoadingIndicator.IsRunning = busy;
    }
}
