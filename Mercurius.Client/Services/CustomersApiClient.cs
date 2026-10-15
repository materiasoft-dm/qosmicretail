using System.Net;
using System.Net.Http.Json;
using Mercurius.Shared.Customers;

namespace Mercurius.Client.Services
{
    public class CustomersApiClient
    {
        private readonly HttpClient _http;

        public CustomersApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<CustomerListResultDto> GetAsync(string? search, int page, int pageSize, CancellationToken ct = default)
        {
            var url = $"api/customers?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}";
            return await _http.GetFromJsonAsync<CustomerListResultDto>(url, ct) ?? new CustomerListResultDto();
        }

        public async Task<CustomerDetailDto?> GetDetailAsync(int id)
        {
            var response = await _http.GetAsync($"api/customers/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CustomerDetailDto>();
        }

        public async Task<(bool Success, string? Error)> CreateAsync(SaveCustomerRequest request) =>
            await SendAsync(_http.PostAsJsonAsync("api/customers", request), "Failed to add the customer.");

        public async Task<(bool Success, string? Error)> UpdateAsync(int id, SaveCustomerRequest request) =>
            await SendAsync(_http.PutAsJsonAsync($"api/customers/{id}", request), "Failed to save the customer.");

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
            return (false, response.StatusCode == HttpStatusCode.Forbidden ? "You don't have permission to do that." : fallback);
        }
    }
}
