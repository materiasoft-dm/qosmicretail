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

    // ---- BIR terminal (Api/BirSyncController)

    public async Task<TerminalConfigDto> RegisterTerminalAsync(TerminalRegisterRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sync/terminal/register", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TerminalConfigDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("The server returned no terminal.");
    }

    // Null result + message when the server refuses (e.g. no permission), rather than throwing,
    // so the Settings page can say why.
    public async Task<(TerminalConfigDto? Config, string? Error)> UpdateTerminalRegistrationAsync(TerminalRegistrationUpdate update, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/sync/terminal/registration", update, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<TerminalConfigDto>(cancellationToken: ct), null);
        }
        var error = await ReadErrorAsync(response, ct);
        return (null, error ?? $"The server refused the change ({(int)response.StatusCode}).");
    }

    public async Task<List<DiscountTypeDto>> GetDiscountTypesAsync(CancellationToken ct = default)
    {
        return await _httpClient.GetFromJsonAsync<List<DiscountTypeDto>>("api/sync/discount-types", ct) ?? new List<DiscountTypeDto>();
    }

    public async Task<List<SalePushResult>> PushZReadingsAsync(List<ZReadingPushDto> readings, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sync/zreadings/push", readings, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SalePushResult>>(cancellationToken: ct) ?? new List<SalePushResult>();
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: ct);
            return body != null && body.TryGetValue("error", out var e) ? e?.ToString() : null;
        }
        catch
        {
            return null;
        }
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
