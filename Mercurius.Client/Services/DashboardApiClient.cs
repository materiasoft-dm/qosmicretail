using System.Net.Http.Json;
using Mercurius.Shared.Dashboard;

namespace Mercurius.Client.Services
{
    public class DashboardApiClient
    {
        private readonly HttpClient _http;
        public DashboardApiClient(HttpClient http) => _http = http;

        public async Task<DashboardDto?> GetAsync() => await _http.GetFromJsonAsync<DashboardDto>("api/dashboard");
    }
}
