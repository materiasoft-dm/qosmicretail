using Mercurius.Mobile.Models;

namespace Mercurius.Mobile.Services;

// Wraps SecureStorage for the JWT + basic profile info. Kept as a small in-memory cache too since
// SecureStorage access is async and the token is read on every authenticated API call.
public class SessionService
{
    private const string TokenKey = "auth_token";
    private const string ExpiresAtKey = "auth_expires_at";
    private const string UserIdKey = "auth_user_id";
    private const string EmailKey = "auth_email";
    private const string FullNameKey = "auth_full_name";

    private string? _cachedToken;

    public async Task SaveSessionAsync(LoginResult login)
    {
        _cachedToken = login.Token;
        await SecureStorage.Default.SetAsync(TokenKey, login.Token);
        await SecureStorage.Default.SetAsync(ExpiresAtKey, login.ExpiresAtUtc.ToString("O"));
        await SecureStorage.Default.SetAsync(UserIdKey, login.UserId);
        await SecureStorage.Default.SetAsync(EmailKey, login.Email);
        await SecureStorage.Default.SetAsync(FullNameKey, login.FullName);
    }

    public async Task<string?> GetTokenAsync()
    {
        if (_cachedToken != null) return _cachedToken;
        _cachedToken = await SecureStorage.Default.GetAsync(TokenKey);
        return _cachedToken;
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return false;

        var expiresRaw = await SecureStorage.Default.GetAsync(ExpiresAtKey);
        if (DateTime.TryParse(expiresRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt))
        {
            return expiresAt > DateTime.UtcNow;
        }
        return false;
    }

    public async Task<string> GetFullNameAsync() => await SecureStorage.Default.GetAsync(FullNameKey) ?? "";
    public async Task<string> GetEmailAsync() => await SecureStorage.Default.GetAsync(EmailKey) ?? "";

    public void ClearSession()
    {
        _cachedToken = null;
        SecureStorage.Default.RemoveAll();
    }
}
