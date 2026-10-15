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

        // sortBy: number | date | due | status | paid (see InvoicesApiController.Get).
        public async Task<InvoiceListResultDto> GetAsync(string? search, int page, int pageSize,
            string? sortBy = null, bool desc = true, CancellationToken ct = default)
        {
            var url = $"api/invoice-list?search={Uri.EscapeDataString(search ?? string.Empty)}&page={page}&pageSize={pageSize}"
                + $"&sortBy={Uri.EscapeDataString(sortBy ?? string.Empty)}&desc={(desc ? "true" : "false")}";
            var result = await _http.GetFromJsonAsync<InvoiceListResultDto>(url, ct);
            return result ?? new InvoiceListResultDto();
        }

        public async Task<InvoiceDetailDto?> GetDetailAsync(int id) => await _http.GetFromJsonAsync<InvoiceDetailDto>($"api/invoice-list/{id}");

        public async Task<(bool Success, string? Error)> CreateRefundAsync(CreateRefundRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/invoice-list/refund", request);
            return response.IsSuccessStatusCode ? (true, null) : (false, await ApiErrors.ReadAsync(response, "Refund failed."));
        }
    }
}
