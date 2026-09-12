using System.Net.Http.Json;
using Mercurius.Shared.MedicineBatches;

namespace Mercurius.Client.Services
{
    public class MedicineBatchesApiClient
    {
        private readonly HttpClient _http;
        public MedicineBatchesApiClient(HttpClient http) => _http = http;

        public async Task<MedicineBatchListResultDto?> GetAsync(int productId) =>
            await _http.GetFromJsonAsync<MedicineBatchListResultDto>($"api/medicine-batches?productId={productId}");

        public async Task<(bool Success, string? Error)> CreateAsync(CreateMedicineBatchRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/medicine-batches", request);
            if (response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body != null && body.TryGetValue("error", out var e) ? e : "Failed to create batch.");
        }

        public async Task DeleteAsync(int id) => await _http.DeleteAsync($"api/medicine-batches/{id}");
    }
}
