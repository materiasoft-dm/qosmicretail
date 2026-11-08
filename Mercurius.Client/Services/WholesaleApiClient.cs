using System.Net.Http.Json;
using Mercurius.Shared.Sales;
using Mercurius.Shared.Wholesale;

namespace Mercurius.Client.Services
{
    public class WholesaleApiClient
    {
        private readonly HttpClient _http;
        public WholesaleApiClient(HttpClient http) => _http = http;

        // sortBy: number | date | due (see WholesaleApiController.Get).
        public async Task<WholesaleOrderListResultDto> GetAsync(string? search, int page, int pageSize,
            string? sortBy = null, bool desc = true, CancellationToken ct = default)
        {
            var url = $"api/wholesale?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}"
                + $"&sortBy={Uri.EscapeDataString(sortBy ?? string.Empty)}&desc={(desc ? "true" : "false")}";
            var result = await _http.GetFromJsonAsync<WholesaleOrderListResultDto>(url, ct);
            return result ?? new WholesaleOrderListResultDto();
        }

        public async Task<WholesaleOrderDetailDto?> GetDetailAsync(int id)
        {
            var response = await _http.GetAsync($"api/wholesale/{id}");
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<WholesaleOrderDetailDto>() : null;
        }

        public async Task<List<WholesaleProductOptionDto>> GetProductsAsync(string? search, CancellationToken ct = default) =>
            await _http.GetFromJsonAsync<List<WholesaleProductOptionDto>>("api/wholesale/products?search=" + Uri.EscapeDataString(search ?? string.Empty), ct) ?? new();

        public async Task<List<CustomerOptionDto>> GetCustomersAsync(string? search, CancellationToken ct = default) =>
            await _http.GetFromJsonAsync<List<CustomerOptionDto>>("api/wholesale/customers?search=" + Uri.EscapeDataString(search ?? string.Empty), ct) ?? new();

        public async Task<(CreatedWholesaleOrderDto? Order, string? Error)> CreateAsync(CreateWholesaleOrderRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/wholesale", request);
            return response.IsSuccessStatusCode
                ? (await response.Content.ReadFromJsonAsync<CreatedWholesaleOrderDto>(), null)
                : (null, await ApiErrors.ReadAsync(response, "Couldn't create the wholesale order."));
        }

        public async Task<(WholesaleOrderDetailDto? Order, string? Error)> RecordPaymentAsync(int id, decimal amount)
        {
            var response = await _http.PostAsJsonAsync($"api/wholesale/{id}/payments", new RecordWholesalePaymentRequest { Amount = amount });
            return response.IsSuccessStatusCode
                ? (await response.Content.ReadFromJsonAsync<WholesaleOrderDetailDto>(), null)
                : (null, await ApiErrors.ReadAsync(response, "Couldn't record the payment."));
        }
    }
}
