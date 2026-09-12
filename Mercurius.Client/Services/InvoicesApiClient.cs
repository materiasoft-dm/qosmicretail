using System.Net.Http.Json;
using Mercurius.Shared.Invoices;

namespace Mercurius.Client.Services
{
    public class InvoicesApiClient
    {
        private readonly HttpClient _http;

        public InvoicesApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<InvoiceListResultDto> GetAsync(string? search, int page, int pageSize)
        {
            var url = $"api/invoice-list?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}";
            var result = await _http.GetFromJsonAsync<InvoiceListResultDto>(url);
            return result ?? new InvoiceListResultDto();
        }

        public async Task<InvoiceDetailDto?> GetDetailAsync(int id) => await _http.GetFromJsonAsync<InvoiceDetailDto>($"api/invoice-list/{id}");

        public async Task<(bool Success, string? Error)> CreateRefundAsync(CreateRefundRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/invoice-list/refund", request);
            if (response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body != null && body.TryGetValue("error", out var e) ? e : "Refund failed.");
        }
    }
}
