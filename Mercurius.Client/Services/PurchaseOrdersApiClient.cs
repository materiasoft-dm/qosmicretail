using System.Net;
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

        public async Task<PurchaseOrderDetailDto?> GetDetailAsync(int id)
        {
            var response = await _http.GetAsync($"api/purchase-orders/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>();
        }

        public async Task<List<SupplierOptionDto>> GetSuppliersAsync() =>
            await _http.GetFromJsonAsync<List<SupplierOptionDto>>("api/purchase-orders/suppliers") ?? new();

        public async Task<List<ProductOptionDto>> GetProductsAsync(string? search, CancellationToken ct = default) =>
            await _http.GetFromJsonAsync<List<ProductOptionDto>>("api/purchase-orders/products?search=" + Uri.EscapeDataString(search ?? string.Empty), ct) ?? new();

        public async Task<(int? Id, string? Error)> CreateAsync(CreatePurchaseOrderRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/purchase-orders", request);
            if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response, "Failed to create purchase order."));
            var body = await response.Content.ReadFromJsonAsync<CreatedOrderDto>();
            return (body?.Id, null);
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(int id, UpdatePurchaseOrderRequest request)
        {
            var response = await _http.PutAsJsonAsync($"api/purchase-orders/{id}", request);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await ReadErrorAsync(response, "Failed to save purchase order."));
        }

        public Task<(bool Success, string? Error)> ApproveAsync(int id) => PostTransitionAsync($"api/purchase-orders/{id}/approve");
        public Task<(bool Success, string? Error)> MarkSentAsync(int id) => PostTransitionAsync($"api/purchase-orders/{id}/mark-sent");
        public Task<(bool Success, string? Error)> MarkReceivedAsync(int id, bool isComplete) =>
            PostTransitionAsync($"api/purchase-orders/{id}/mark-received?isComplete={(isComplete ? "true" : "false")}");

        private async Task<(bool Success, string? Error)> PostTransitionAsync(string url)
        {
            var response = await _http.PostAsync(url, null);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, await ReadErrorAsync(response, "The order couldn't be updated."));
        }

        private static async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden) return "You don't have permission to do that.";
            try
            {
                var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                if (body != null && body.TryGetValue("error", out var e) && e != null) return e.ToString() ?? fallback;
            }
            catch
            {
                // Not a JSON error body.
            }
            return fallback;
        }

        private sealed class CreatedOrderDto
        {
            public int Id { get; set; }
        }
    }
}
