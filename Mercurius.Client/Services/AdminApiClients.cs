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

        public async Task<(bool Success, string? Error)> CreateAsync(CreateUserRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/users", request);
            return response.IsSuccessStatusCode ? (true, null) : (false, await ApiErrors.ReadAsync(response, "The user couldn't be added."));
        }

        public async Task<(bool Success, string? Error)> AssignRolesAsync(string id, List<string> roleIds)
        {
            var response = await _http.PostAsJsonAsync($"api/users/{id}/roles", new AssignUserRolesRequest { RoleIds = roleIds });
            return response.IsSuccessStatusCode ? (true, null) : (false, await ApiErrors.ReadAsync(response, "The roles couldn't be saved."));
        }

        public async Task<(bool Success, string? Error)> SetActiveAsync(string id, bool activate)
        {
            var response = await _http.PostAsJsonAsync($"api/users/{id}/active", new SetUserActiveRequest { Activate = activate });
            return response.IsSuccessStatusCode ? (true, null) : (false, await ApiErrors.ReadAsync(response, "The user couldn't be updated."));
        }

        public async Task<(bool Success, string? Error)> ResetPasswordAsync(string id, string newPassword)
        {
            var response = await _http.PostAsJsonAsync($"api/users/{id}/password", new ResetUserPasswordRequest { NewPassword = newPassword });
            return response.IsSuccessStatusCode ? (true, null) : (false, await ApiErrors.ReadAsync(response, "The password couldn't be reset."));
        }
    }

    public class LogsApiClient
    {
        private readonly HttpClient _http;
        public LogsApiClient(HttpClient http) => _http = http;

        public async Task<List<LogFileDto>> GetAsync() => await _http.GetFromJsonAsync<List<LogFileDto>>("api/logs") ?? new();
        public async Task<List<LogLineDto>> GetLinesAsync(string fileName) => await _http.GetFromJsonAsync<List<LogLineDto>>($"api/logs/{Uri.EscapeDataString(fileName)}") ?? new();
        public async Task ClearAsync() => await _http.DeleteAsync("api/logs");
    }

    public class AdminToolsApiClient
    {
        private readonly HttpClient _http;
        public AdminToolsApiClient(HttpClient http) => _http = http;

        public async Task<(DataQueryResultDto? Result, string? Error)> ExecuteQueryAsync(string apiKey, string sql)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "api/admin/data-query")
            {
                Content = JsonContent.Create(new DataQueryRequest { ApiKey = apiKey, Sql = sql })
            };
            // This endpoint answers 401 for a wrong admin key, not just for a dead session, so a bad
            // key must show its error in place instead of bouncing the user to the sign-in page.
            request.Options.Set(ApiRequestOptions.Suppress401Redirect, true);

            var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode) return (await response.Content.ReadFromJsonAsync<DataQueryResultDto>(), null);
            return (null, await ApiErrors.ReadAsync(response, "The query failed."));
        }

        public async Task<DatabaseBackupsDto> GetBackupsAsync() =>
            await _http.GetFromJsonAsync<DatabaseBackupsDto>("api/admin/backups") ?? new();

        public async Task<List<TenantDto>> GetTenantsAsync() =>
            await _http.GetFromJsonAsync<List<TenantDto>>("api/platform/tenants") ?? new();

        public async Task<(TenantDto? Tenant, string? Error)> CreateTenantAsync(CreateTenantRequestDto request)
        {
            var response = await _http.PostAsJsonAsync("api/platform/tenants", request);
            if (response.IsSuccessStatusCode) return (await response.Content.ReadFromJsonAsync<TenantDto>(), null);
            return (null, await ApiErrors.ReadAsync(response, "Failed to create the tenant."));
        }
    }

    // Criteria for one audit trail search. From/To are the viewer's local calendar dates; the
    // client turns them into a UTC [from, to + 1 day) window so "today" means the viewer's today.
    public class AuditTrailCriteria
    {
        public string? Search { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Action { get; set; }
        public string? Source { get; set; }
        public string? EntityType { get; set; }
        public string? Branch { get; set; }
    }

    public class AuditTrailApiClient
    {
        private readonly HttpClient _http;
        public AuditTrailApiClient(HttpClient http) => _http = http;

        public async Task<AuditTrailFiltersDto> GetFiltersAsync() =>
            await _http.GetFromJsonAsync<AuditTrailFiltersDto>("api/audit-trail/filters") ?? new();

        public async Task<AuditLogPageDto> GetAsync(AuditTrailCriteria criteria, int page, int pageSize, bool oldestFirst, CancellationToken ct = default)
        {
            var url = $"api/audit-trail?page={page}&pageSize={pageSize}&oldestFirst={(oldestFirst ? "true" : "false")}";
            if (criteria.FromDate.HasValue) url += "&fromUtc=" + Uri.EscapeDataString(LocalDayStartUtc(criteria.FromDate.Value).ToString("o"));
            if (criteria.ToDate.HasValue) url += "&toUtc=" + Uri.EscapeDataString(LocalDayStartUtc(criteria.ToDate.Value.AddDays(1)).ToString("o"));
            url += Param("action", criteria.Action) + Param("source", criteria.Source) + Param("entityType", criteria.EntityType)
                 + Param("branch", criteria.Branch) + Param("search", criteria.Search);
            return await _http.GetFromJsonAsync<AuditLogPageDto>(url, ct) ?? new();
        }

        public async Task<AuditLogDetailDto?> GetDetailAsync(long id) =>
            await _http.GetFromJsonAsync<AuditLogDetailDto>($"api/audit-trail/{id}");

        private static DateTime LocalDayStartUtc(DateTime date) =>
            DateTime.SpecifyKind(date.Date, DateTimeKind.Local).ToUniversalTime();

        private static string Param(string name, string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : $"&{name}={Uri.EscapeDataString(value.Trim())}";
    }
}
