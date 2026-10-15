using System.Net.Http.Json;
using Mercurius.Shared.Dashboard;

namespace Mercurius.Client.Services
{
    public class DashboardApiClient
    {
        private readonly HttpClient _http;
        public DashboardApiClient(HttpClient http) => _http = http;

        // Sends this device's UTC offset so "today" and "this month" are the viewer's own.
        public async Task<DashboardDto?> GetAsync()
        {
            var offset = (int)DateTimeOffset.Now.Offset.TotalMinutes;
            return await _http.GetFromJsonAsync<DashboardDto>($"api/dashboard?utcOffsetMinutes={offset}");
        }

        public async Task<List<DashboardWidgetLayoutDto>> GetLayoutAsync() =>
            await _http.GetFromJsonAsync<List<DashboardWidgetLayoutDto>>("api/dashboard/layout") ?? new();

        public async Task<bool> SaveLayoutAsync(List<DashboardWidgetLayoutDto> layout) =>
            (await _http.PutAsJsonAsync("api/dashboard/layout", layout)).IsSuccessStatusCode;
    }
}
