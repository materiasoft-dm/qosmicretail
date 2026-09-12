using System.Net.Http.Json;
using Mercurius.Shared.Sales;

namespace Mercurius.Client.Services
{
    public class SalesApiClient
    {
        private readonly HttpClient _http;
        public SalesApiClient(HttpClient http) => _http = http;

        public async Task<List<CustomerOptionDto>> GetCustomersAsync(string? search) =>
            await _http.GetFromJsonAsync<List<CustomerOptionDto>>("api/sales/customers?search=" + Uri.EscapeDataString(search ?? string.Empty)) ?? new();

        public async Task<(bool Success, CheckoutResultDto? Result, string? Error)> CheckoutAsync(CheckoutRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/sales/checkout", request);
            if (response.IsSuccessStatusCode)
            {
                return (true, await response.Content.ReadFromJsonAsync<CheckoutResultDto>(), null);
            }
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, null, body != null && body.TryGetValue("error", out var e) ? e : "Checkout failed.");
        }
    }
}
