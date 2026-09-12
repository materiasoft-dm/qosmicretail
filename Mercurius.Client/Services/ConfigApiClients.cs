using System.Net.Http.Json;
using Mercurius.Shared.Common;
using Mercurius.Shared.Configuration;

namespace Mercurius.Client.Services
{
    public class AdjustmentReasonsApiClient
    {
        private readonly HttpClient _http;
        public AdjustmentReasonsApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<AdjustmentReasonDto>> GetAsync(string? search = null)
        {
            var url = "api/adjustment-reasons?search=" + Uri.EscapeDataString(search ?? string.Empty);
            return await _http.GetFromJsonAsync<ListResultDto<AdjustmentReasonDto>>(url) ?? new();
        }

        public async Task<AdjustmentReasonDto?> CreateAsync(CreateAdjustmentReasonRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/adjustment-reasons", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdjustmentReasonDto>() : null;
        }
    }

    public class ProductCategoriesApiClient
    {
        private readonly HttpClient _http;
        public ProductCategoriesApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<ProductCategoryDto>> GetAsync(string? search = null)
        {
            var url = "api/product-categories?search=" + Uri.EscapeDataString(search ?? string.Empty);
            return await _http.GetFromJsonAsync<ListResultDto<ProductCategoryDto>>(url) ?? new();
        }

        public async Task<ProductCategoryDto?> CreateAsync(CreateProductCategoryRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/product-categories", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProductCategoryDto>() : null;
        }
    }

    public class LocationsApiClient
    {
        private readonly HttpClient _http;
        public LocationsApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<LocationDto>> GetAsync(string? search = null)
        {
            var url = "api/locations?search=" + Uri.EscapeDataString(search ?? string.Empty);
            return await _http.GetFromJsonAsync<ListResultDto<LocationDto>>(url) ?? new();
        }

        public async Task<LocationDto?> CreateAsync(CreateLocationRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/locations", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<LocationDto>() : null;
        }
    }

    public class CategoryFieldsApiClient
    {
        private readonly HttpClient _http;
        public CategoryFieldsApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<CategoryFieldDto>> GetAsync()
        {
            return await _http.GetFromJsonAsync<ListResultDto<CategoryFieldDto>>("api/category-fields") ?? new();
        }

        public async Task<List<ProductCategoryDto>> GetCategoriesAsync()
        {
            return await _http.GetFromJsonAsync<List<ProductCategoryDto>>("api/category-fields/categories") ?? new();
        }

        public async Task<(bool Success, string? Error)> CreateAsync(CreateCategoryFieldRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/category-fields", request);
            if (response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body != null && body.TryGetValue("error", out var e) ? e : "Failed to create field.");
        }
    }
}
