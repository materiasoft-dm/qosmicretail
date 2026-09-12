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
    }
}
