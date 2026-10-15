using System.Net.Http.Json;
using Mercurius.Mobile.Models;

namespace Mercurius.Mobile.Services;

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public LoginResult? Login { get; set; }
}

public class AuthApiService
{
    private readonly HttpClient _httpClient;

    public AuthApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { Email = email, Password = password }, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new AuthResult { Success = false, ErrorMessage = "Invalid email or password." };
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResult>(cancellationToken: ct);
            if (result == null)
            {
                return new AuthResult { Success = false, ErrorMessage = "Unexpected response from server." };
            }

            return new AuthResult { Success = true, Login = result };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, ErrorMessage = $"Couldn't reach the server: {ex.Message}" };
        }
    }

    // Tells the server this user signed out, purely for its audit trail. Best-effort with a short
    // timeout — signing out must still work offline, it just won't be recorded then.
    public async Task NotifyLogoutAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _httpClient.PostAsync("api/auth/logout", null, cts.Token);
        }
        catch
        {
            // Offline or server unreachable — not worth blocking sign-out over.
        }
    }
}
