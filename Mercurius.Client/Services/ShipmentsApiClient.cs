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

        public async Task<bool> CreateAsync(CreateShipmentRequest request) =>
            (await _http.PostAsJsonAsync("api/shipments", request)).IsSuccessStatusCode;
    }
}
