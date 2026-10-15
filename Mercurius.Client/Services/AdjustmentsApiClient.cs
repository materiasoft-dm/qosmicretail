using System.Net.Http.Json;
using Mercurius.Shared.Adjustments;
using Mercurius.Shared.Common;

namespace Mercurius.Client.Services
{
    public class AdjustmentsApiClient
    {
        private readonly HttpClient _http;
        public AdjustmentsApiClient(HttpClient http) => _http = http;

        // sortBy: date | quantity (see AdjustmentsApiController.Get).
        public async Task<ListResultDto<AdjustmentListItemDto>> GetAsync(string? search, int page, int pageSize,
            string? sortBy = null, bool desc = true, CancellationToken ct = default)
        {
            var url = $"api/adjustments?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}"
                + $"&sortBy={Uri.EscapeDataString(sortBy ?? string.Empty)}&desc={(desc ? "true" : "false")}";
            return await _http.GetFromJsonAsync<ListResultDto<AdjustmentListItemDto>>(url, ct) ?? new();
        }

        public async Task<List<AdjustmentReasonOptionDto>> GetReasonsAsync() =>
            await _http.GetFromJsonAsync<List<AdjustmentReasonOptionDto>>("api/adjustments/reasons") ?? new();

        public async Task<List<ProductOptionDto>> GetProductsAsync(string? search, CancellationToken ct = default) =>
            await _http.GetFromJsonAsync<List<ProductOptionDto>>("api/adjustments/products?search=" + Uri.EscapeDataString(search ?? string.Empty), ct) ?? new();

        public async Task<(bool Success, string? Error)> CreateAsync(CreateAdjustmentRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/adjustments", request);
            return response.IsSuccessStatusCode ? (true, null) : (false, await ApiErrors.ReadAsync(response, "Failed to create adjustment."));
        }
    }
}
