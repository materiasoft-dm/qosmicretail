using System.Net.Http.Json;
using Mercurius.Shared.Common;
using Mercurius.Shared.PurchaseOrders;

namespace Mercurius.Client.Services
{
    public class PurchaseOrdersApiClient
    {
        private readonly HttpClient _http;
        public PurchaseOrdersApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<PurchaseOrderListItemDto>> GetAsync() =>
            await _http.GetFromJsonAsync<ListResultDto<PurchaseOrderListItemDto>>("api/purchase-orders") ?? new();

        public async Task<List<SupplierOptionDto>> GetSuppliersAsync() =>
            await _http.GetFromJsonAsync<List<SupplierOptionDto>>("api/purchase-orders/suppliers") ?? new();

        public async Task<List<ProductOptionDto>> GetProductsAsync(string? search) =>
            await _http.GetFromJsonAsync<List<ProductOptionDto>>("api/purchase-orders/products?search=" + Uri.EscapeDataString(search ?? string.Empty)) ?? new();

        public async Task<(bool Success, string? Error)> CreateAsync(CreatePurchaseOrderRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/purchase-orders", request);
            if (response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body != null && body.TryGetValue("error", out var e) ? e : "Failed to create purchase order.");
        }

        public async Task<bool> ApproveAsync(int id) => (await _http.PostAsync($"api/purchase-orders/{id}/approve", null)).IsSuccessStatusCode;
        public async Task<bool> MarkSentAsync(int id) => (await _http.PostAsync($"api/purchase-orders/{id}/mark-sent", null)).IsSuccessStatusCode;
        public async Task<bool> MarkReceivedAsync(int id, bool isComplete) => (await _http.PostAsync($"api/purchase-orders/{id}/mark-received?isComplete={isComplete}", null)).IsSuccessStatusCode;
    }
}
