using Mercurius.Shared.Sales;
using Microsoft.JSInterop;

namespace Mercurius.Client.Services
{
    // One change made on this device that the server hasn't confirmed yet. Everything that can be
    // done offline goes through here, so it's all sent the same way, in order, by SyncService.
    public sealed class OutboxEntry
    {
        public const string Pending = "pending";   // waiting to be sent
        public const string Failed = "failed";     // the server refused it; needs a person (Outbox page)
        public const string Conflict = "conflict"; // someone changed the same record first; needs a decision

        // Also sent as the Idempotency-Key header, so a retry after a lost response is applied once.
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Kind { get; set; } = string.Empty;    // "sale", later "refund", "adjustment", ...
        public string Label { get; set; } = string.Empty;   // what the person sees: "Sale · 3 items · ₱120.00"
        public string Method { get; set; } = "POST";
        public string Url { get; set; } = string.Empty;     // relative API address
        public string Body { get; set; } = string.Empty;    // JSON
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = Pending;
        public string? Error { get; set; }
        public int Attempts { get; set; }
        public DateTime? LastAttemptUtc { get; set; }
        // For a conflict on a product edit: the version the record is at now, so "overwrite with mine" can retarget the edit.
        public int? ConflictVersion { get; set; }
    }

    // Thin wrapper over wwwroot/js/offlineStore.js: the device's product catalog and its outbox.
    public class OfflineStoreService
    {
        private readonly IJSRuntime _js;
        public OfflineStoreService(IJSRuntime js) => _js = js;

        public Task ReplaceProductsAsync(List<OfflineProductDto> products) =>
            _js.InvokeVoidAsync("mercuriusOfflineStore.replaceProducts", products).AsTask();

        public async Task<List<OfflineProductDto>> GetAllProductsAsync() =>
            await _js.InvokeAsync<List<OfflineProductDto>>("mercuriusOfflineStore.getAllProducts");

        public Task DecrementProductStockAsync(int productId, int quantity) =>
            _js.InvokeVoidAsync("mercuriusOfflineStore.decrementProductStock", productId, quantity).AsTask();

        public Task PutOutboxEntryAsync(OutboxEntry entry) =>
            _js.InvokeVoidAsync("mercuriusOfflineStore.putOutboxEntry", entry).AsTask();

        public async Task<List<OutboxEntry>> GetOutboxAsync() =>
            await _js.InvokeAsync<List<OutboxEntry>>("mercuriusOfflineStore.getOutbox");

        public Task RemoveOutboxEntryAsync(string id) =>
            _js.InvokeVoidAsync("mercuriusOfflineStore.removeOutboxEntry", id).AsTask();

        // The queue from before the outbox existed; read once at start to move its sales across.
        public async Task<List<OfflineSaleRequest>> GetLegacyPendingSalesAsync() =>
            await _js.InvokeAsync<List<OfflineSaleRequest>>("mercuriusOfflineStore.getPendingSales");

        public Task RemoveLegacyPendingSaleAsync(Guid syncId) =>
            _js.InvokeVoidAsync("mercuriusOfflineStore.removePendingSale", syncId).AsTask();
    }
}
