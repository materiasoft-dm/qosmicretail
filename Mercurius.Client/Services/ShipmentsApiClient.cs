using System.Net.Http.Json;
using Mercurius.Shared.Common;
using Mercurius.Shared.Shipments;

namespace Mercurius.Client.Services
{
    public class ShipmentsApiClient
    {
        private readonly HttpClient _http;
        public ShipmentsApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<ShipmentListItemDto>> GetAsync() =>
            await _http.GetFromJsonAsync<ListResultDto<ShipmentListItemDto>>("api/shipments") ?? new();

        public async Task<List<SupplierOptionDto>> GetSuppliersAsync() =>
            await _http.GetFromJsonAsync<List<SupplierOptionDto>>("api/shipments/suppliers") ?? new();

        public async Task<List<PurchaseOrderOptionDto>> GetPurchaseOrdersAsync() =>
            await _http.GetFromJsonAsync<List<PurchaseOrderOptionDto>>("api/shipments/purchase-orders") ?? new();

        public async Task<List<ShipmentStatusDto>> GetStatusesAsync() =>
            await _http.GetFromJsonAsync<List<ShipmentStatusDto>>("api/shipments/statuses") ?? new();

        public async Task<List<ReceivableLineDto>> GetPurchaseOrderLinesAsync(int purchaseOrderId) =>
            await _http.GetFromJsonAsync<List<ReceivableLineDto>>($"api/shipments/purchase-orders/{purchaseOrderId}/lines") ?? new();

        public async Task<List<ReceivableProductDto>> SearchProductsAsync(string? search, CancellationToken ct = default) =>
            await _http.GetFromJsonAsync<List<ReceivableProductDto>>($"api/shipments/products?search={Uri.EscapeDataString(search ?? string.Empty)}", ct) ?? new();

        public async Task<(bool Success, string? Error)> CreateAsync(CreateShipmentRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/shipments", request);
            return response.IsSuccessStatusCode
                ? (true, null)
                : (false, await ApiErrors.ReadAsync(response, "The shipment couldn't be saved."));
        }
    }
}
