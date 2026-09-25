using System.Net.Http.Json;
using Mercurius.Mobile.Models;
using Microsoft.Extensions.Http;

namespace Mercurius.Mobile.Services;

// Talks to Api/SyncController on the server. Requests go through the "MercuriusApi" named
// HttpClient, which has AuthHeaderHandler attaching the bearer token automatically.
public class SyncApiService
{
    private readonly HttpClient _httpClient;

    public SyncApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("MercuriusApi");
    }

    public async Task<List<ProductDto>> PullProductsAsync(DateTime since, CancellationToken ct = default)
    {
        var url = $"api/sync/products/pull?since={Uri.EscapeDataString(since.ToString("O"))}";
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>(cancellationToken: ct);
        return products ?? new List<ProductDto>();
    }

    public async Task<List<SalePushResult>> PushProductsAsync(List<ProductPushDto> products, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sync/products/push", products, ct);
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<SalePushResult>>(cancellationToken: ct);
        return results ?? new List<SalePushResult>();
    }

    public async Task<List<SalePushResult>> PushSalesAsync(List<SalePushDto> sales, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sync/invoices/push", sales, ct);
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<SalePushResult>>(cancellationToken: ct);
        return results ?? new List<SalePushResult>();
    }

    public async Task<ReceiptPullPage> PullReceiptsAsync(string? cursor, DateTime fromDateUtc, int take, CancellationToken ct = default)
    {
        var url = $"api/sync/receipts/pull?take={take}&fromDate={Uri.EscapeDataString(fromDateUtc.ToString("O"))}";
        if (!string.IsNullOrEmpty(cursor)) url += $"&cursor={Uri.EscapeDataString(cursor)}";
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReceiptPullPage>(cancellationToken: ct) ?? new ReceiptPullPage();
    }

    // Backs SettingsPage's clinic picker.
    public async Task<List<LocationDto>> GetLocationsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("api/sync/locations", ct);
        response.EnsureSuccessStatusCode();
        var locations = await response.Content.ReadFromJsonAsync<List<LocationDto>>(cancellationToken: ct);
        return locations ?? new List<LocationDto>();
    }
}
