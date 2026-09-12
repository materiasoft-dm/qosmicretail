using System.Net.Http.Json;
using Mercurius.Shared.Customers;

namespace Mercurius.Client.Services
{
    public class CustomersApiClient
    {
        private readonly HttpClient _http;

        public CustomersApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<CustomerListResultDto> GetAsync(string? search, int page, int pageSize)
        {
            var url = $"api/customers?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}";
            var result = await _http.GetFromJsonAsync<CustomerListResultDto>(url);
            return result ?? new CustomerListResultDto();
        }

        public async Task<CustomerListItemDto?> CreateAsync(CreateCustomerRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/customers", request);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<CustomerListItemDto>();
        }
    }
}
