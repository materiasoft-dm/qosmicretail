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

        public async Task<bool> UpdateAsync(int id, UpdateAdjustmentReasonRequest request) =>
            (await _http.PutAsJsonAsync($"api/adjustment-reasons/{id}", request)).IsSuccessStatusCode;
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

        public async Task<bool> UpdateAsync(int id, UpdateProductCategoryRequest request) =>
            (await _http.PutAsJsonAsync($"api/product-categories/{id}", request)).IsSuccessStatusCode;
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

        public async Task<bool> UpdateAsync(int id, UpdateLocationRequest request) =>
            (await _http.PutAsJsonAsync($"api/locations/{id}", request)).IsSuccessStatusCode;
    }

    public class RefundReasonsApiClient
    {
        private readonly HttpClient _http;
        public RefundReasonsApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<RefundReasonDto>> GetAsync(string? search = null)
        {
            var url = "api/refund-reasons?search=" + Uri.EscapeDataString(search ?? string.Empty);
            return await _http.GetFromJsonAsync<ListResultDto<RefundReasonDto>>(url) ?? new();
        }

        public async Task<RefundReasonDto?> CreateAsync(CreateRefundReasonRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/refund-reasons", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<RefundReasonDto>() : null;
        }

        public async Task<bool> UpdateAsync(int id, UpdateRefundReasonRequest request) =>
            (await _http.PutAsJsonAsync($"api/refund-reasons/{id}", request)).IsSuccessStatusCode;
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
            return response.IsSuccessStatusCode ? (true, null) : (false, await ApiErrors.ReadAsync(response, "Failed to create field."));
        }

        public async Task<(bool Success, string? Error)> UpdateAsync(int id, UpdateCategoryFieldRequest request)
        {
            var response = await _http.PutAsJsonAsync($"api/category-fields/{id}", request);
            return response.IsSuccessStatusCode ? (true, null) : (false, await ApiErrors.ReadAsync(response, "Failed to save field."));
        }
    }

    public class DiscountTypesApiClient
    {
        private readonly HttpClient _http;
        public DiscountTypesApiClient(HttpClient http) => _http = http;

        public async Task<ListResultDto<DiscountTypeDto>> GetAsync() =>
            await _http.GetFromJsonAsync<ListResultDto<DiscountTypeDto>>("api/discount-types") ?? new();

        public async Task<string?> SaveAsync(DiscountTypeDto request)
        {
            var response = request.Id == 0
                ? await _http.PostAsJsonAsync("api/discount-types", request)
                : await _http.PutAsJsonAsync($"api/discount-types/{request.Id}", request);
            return response.IsSuccessStatusCode ? null : await ApiErrors.ReadAsync(response, "Failed to save.");
        }
    }
}
