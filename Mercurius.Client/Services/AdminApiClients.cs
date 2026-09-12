using System.Net.Http.Json;
using Mercurius.Shared.Admin;
using Mercurius.Shared.Common;

namespace Mercurius.Client.Services
{
    public class RolesApiClient
    {
        private readonly HttpClient _http;
        public RolesApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<RoleDto>> GetAsync() => await _http.GetFromJsonAsync<ListResultDto<RoleDto>>("api/roles") ?? new();
        public async Task<List<string>> GetModulesAsync() => await _http.GetFromJsonAsync<List<string>>("api/roles/modules") ?? new();
        public async Task<RoleDetailDto?> GetDetailAsync(string id) => await _http.GetFromJsonAsync<RoleDetailDto>($"api/roles/{id}");

        public async Task<(bool Success, string? Error)> CreateAsync(CreateRoleRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/roles", request);
            if (response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body != null && body.TryGetValue("error", out var e) ? e : "Failed to create role.");
        }

        public async Task<bool> UpdatePagesAsync(string id, List<string> pages) =>
            (await _http.PutAsJsonAsync($"api/roles/{id}/pages", new UpdateRolePagesRequest { Pages = pages })).IsSuccessStatusCode;

        public async Task<bool> DeleteAsync(string id) => (await _http.DeleteAsync($"api/roles/{id}")).IsSuccessStatusCode;
    }

    public class UsersApiClient
    {
        private readonly HttpClient _http;
        public UsersApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<UserListItemDto>> GetAsync(bool showDeactivated) =>
            await _http.GetFromJsonAsync<ListResultDto<UserListItemDto>>($"api/users?showDeactivated={showDeactivated}") ?? new();

        public async Task<List<RoleDto>> GetRolesAsync() => await _http.GetFromJsonAsync<List<RoleDto>>("api/users/roles") ?? new();

        public async Task<bool> AssignRolesAsync(string id, List<string> roleIds) =>
            (await _http.PostAsJsonAsync($"api/users/{id}/roles", new AssignUserRolesRequest { RoleIds = roleIds })).IsSuccessStatusCode;

        public async Task<(bool Success, string? Error)> SetActiveAsync(string id, bool activate)
        {
            var response = await _http.PostAsJsonAsync($"api/users/{id}/active", new SetUserActiveRequest { Activate = activate });
            if (response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body != null && body.TryGetValue("error", out var e) ? e : "Failed.");
        }
    }

    public class LogsApiClient
    {
        private readonly HttpClient _http;
        public LogsApiClient(HttpClient http) => _http = http;

        public async Task<List<LogFileDto>> GetAsync() => await _http.GetFromJsonAsync<List<LogFileDto>>("api/logs") ?? new();
        public async Task<List<LogLineDto>> GetLinesAsync(string fileName) => await _http.GetFromJsonAsync<List<LogLineDto>>($"api/logs/{fileName}") ?? new();
        public async Task ClearAsync() => await _http.DeleteAsync("api/logs");
    }
}
