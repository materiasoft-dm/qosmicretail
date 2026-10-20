using System.Net.Http.Json;
using Mercurius.Shared.Sales;

namespace Mercurius.Client.Services
{
    public class SalesApiClient
    {
        private readonly HttpClient _http;
        public SalesApiClient(HttpClient http) => _http = http;

        public async Task<List<CustomerOptionDto>> GetCustomersAsync(string? search, CancellationToken ct = default) =>
            await _http.GetFromJsonAsync<List<CustomerOptionDto>>("api/sales/customers?search=" + Uri.EscapeDataString(search ?? string.Empty), ct) ?? new();

        // Throws HttpRequestException only when the server couldn't be reached — the caller's cue
        // to queue the sale offline. Anything the server answered comes back as Success/Error;
        // PriceChange is set (and nothing was charged) when the ticket's prices were out of date.
        public async Task<(bool Success, CheckoutResultDto? Result, string? Error, CheckoutPriceChangeDto? PriceChange)> CheckoutAsync(CheckoutRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/sales/checkout", request);
            if (response.IsSuccessStatusCode)
            {
                return (true, await response.Content.ReadFromJsonAsync<CheckoutResultDto>(), null, null);
            }
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var change = await response.Content.ReadFromJsonAsync<CheckoutPriceChangeDto>();
                if (change != null) return (false, null, change.Error, change);
            }
            return (false, null, await ApiErrors.ReadAsync(response, "Checkout failed."), null);
        }
    }
}
