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
}
