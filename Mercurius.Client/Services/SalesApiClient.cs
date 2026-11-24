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
        // A charge waits at most 15 seconds: on a bad connection the cashier gets "saved offline" instead
        // of a frozen till. That's safe even if the server did get it in the end: the queued copy carries
        // the same SyncId, so sending it later just returns the sale already made.
        public async Task<(bool Success, CheckoutResultDto? Result, string? Error, CheckoutPriceChangeDto? PriceChange)> CheckoutAsync(CheckoutRequest request)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _http.PostAsJsonAsync("api/sales/checkout", request, timeout.Token);
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
