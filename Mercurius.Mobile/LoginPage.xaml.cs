using Mercurius.Mobile.Services;

namespace Mercurius.Mobile;

public partial class LoginPage : ContentPage
{
    private readonly AuthApiService _authApiService;
    private readonly SessionService _sessionService;

    public LoginPage(AuthApiService authApiService, SessionService sessionService)
    {
        InitializeComponent();
        _authApiService = authApiService;
        _sessionService = sessionService;
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
