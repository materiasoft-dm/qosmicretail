using System.Net.Http.Json;
using Mercurius.Shared.Products;

namespace Mercurius.Client.Services
{
    public class ProductsApiClient
    {
        private readonly HttpClient _http;

        public ProductsApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<ProductListResultDto> GetAsync(string? search, int page, int pageSize, int? categoryId = null)
        {
            var url = $"api/products?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}";
            if (categoryId.HasValue && categoryId.Value > 0) url += $"&categoryId={categoryId.Value}";
            var result = await _http.GetFromJsonAsync<ProductListResultDto>(url);
            return result ?? new ProductListResultDto();
        }

        public async Task<ProductDetailDto?> GetDetailAsync(int id) => await _http.GetFromJsonAsync<ProductDetailDto>($"api/products/{id}");

        public async Task<List<Mercurius.Shared.Configuration.ProductCategoryDto>> GetCategoryOptionsAsync() =>
            await _http.GetFromJsonAsync<List<Mercurius.Shared.Configuration.ProductCategoryDto>>("api/products/categories") ?? new();

        public async Task<(bool Success, string? Error)> UpdateAsync(int id, UpdateProductRequest request)
        {
            var response = await _http.PutAsJsonAsync($"api/products/{id}", request);
            if (response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body != null && body.TryGetValue("error", out var e) ? e : "Failed to save product.");
        }

        public async Task<(bool Success, string? Error)> CreateAsync(CreateProductRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/products", request);
            if (response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body != null && body.TryGetValue("error", out var e) ? e : "Failed to create product.");
        }
    }
}
