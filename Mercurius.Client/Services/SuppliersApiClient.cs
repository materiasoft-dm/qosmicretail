using System.Net.Http.Json;
using Mercurius.Shared.Suppliers;

namespace Mercurius.Client.Services
{
    public class SuppliersApiClient
    {
        private readonly HttpClient _http;

        public SuppliersApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<SupplierListResultDto> GetAsync(string? search, int page, int pageSize)
        {
            var url = $"api/suppliers?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}";
            var result = await _http.GetFromJsonAsync<SupplierListResultDto>(url);
            return result ?? new SupplierListResultDto();
        }

        public async Task<SupplierListItemDto?> CreateAsync(CreateSupplierRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/suppliers", request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SupplierListItemDto>();
        }
    }
}
