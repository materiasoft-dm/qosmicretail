using System.Net.Http.Json;
using Mercurius.Shared.Adjustments;
using Mercurius.Shared.Common;

namespace Mercurius.Client.Services
{
    public class AdjustmentsApiClient
    {
        private readonly HttpClient _http;
        public AdjustmentsApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<AdjustmentListItemDto>> GetAsync(string? search, int page, int pageSize)
        {
            var url = $"api/adjustments?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}";
            return await _http.GetFromJsonAsync<ListResultDto<AdjustmentListItemDto>>(url) ?? new();
        }

        public async Task<List<AdjustmentReasonOptionDto>> GetReasonsAsync() =>
            await _http.GetFromJsonAsync<List<AdjustmentReasonOptionDto>>("api/adjustments/reasons") ?? new();

        public async Task<List<ProductOptionDto>> GetProductsAsync(string? search) =>
            await _http.GetFromJsonAsync<List<ProductOptionDto>>("api/adjustments/products?search=" + Uri.EscapeDataString(search ?? string.Empty)) ?? new();

        public async Task<(bool Success, string? Error)> CreateAsync(CreateAdjustmentRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/adjustments", request);
            if (response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body != null && body.TryGetValue("error", out var e) ? e : "Failed to create adjustment.");
        }
    }
}
