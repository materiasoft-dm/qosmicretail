using System.Net;
using System.Net.Http.Json;
using Mercurius.Shared.Account;

namespace Mercurius.Client.Services
{
    // The signed-in user's own account: branch switching and personal settings.
    public class AccountApiClient
    {
        private readonly HttpClient _http;

        public AccountApiClient(HttpClient http) => _http = http;

        public async Task<(BranchOptionDto? Branch, string? Error)> SwitchBranchAsync(int locationId)
        {
            var response = await _http.PostAsJsonAsync("api/account/branch", new SwitchBranchRequest { LocationId = locationId });
            if (response.IsSuccessStatusCode) return (await response.Content.ReadFromJsonAsync<BranchOptionDto>(), null);
            return (null, response.StatusCode == HttpStatusCode.Forbidden ? "That branch isn't available to you." : "Couldn't switch branch.");
        }

        public async Task<AccountProfileDto?> GetProfileAsync() =>
            await _http.GetFromJsonAsync<AccountProfileDto>("api/account/profile");

        public async Task<(bool Success, string? Error)> UpdateProfileAsync(UpdateAccountProfileRequest request) =>
            await SendAsync(_http.PutAsJsonAsync("api/account/profile", request), "Couldn't save your details.");

        public async Task<(bool Success, string? Error)> ChangePasswordAsync(ChangePasswordRequest request) =>
            await SendAsync(_http.PostAsJsonAsync("api/account/change-password", request), "Couldn't change your password.");

        private static async Task<(bool Success, string? Error)> SendAsync(Task<HttpResponseMessage> call, string fallback)
        {
            var response = await call;
            if (response.IsSuccessStatusCode) return (true, null);
            try
            {
                var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                if (body != null && body.TryGetValue("error", out var e) && e != null) return (false, e.ToString());
            }
            catch
            {
                // Not a JSON error body.
            }
            return (false, fallback);
        }
    }
}
