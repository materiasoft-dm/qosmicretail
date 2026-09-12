using System.Net.Http.Json;
using Mercurius.Shared.Sales;

namespace Mercurius.Client.Services
{
    // Orchestrates the two halves of offline selling: pulling a product catalog snapshot down
    // for local search while offline, and pushing sales that were queued locally back up once a
    // connection is available. Both directions are best-effort — any failure here just means
    // "still offline" or "still pending," never a hard error the UI needs to surface loudly.
    public class OfflineSyncService
    {
        private readonly HttpClient _http;
        private readonly OfflineStoreService _store;

        public OfflineSyncService(HttpClient http, OfflineStoreService store)
        {
            _http = http;
            _store = store;
        }

        public async Task<bool> PullProductCatalogAsync()
        {
            try
            {
                var products = await _http.GetFromJsonAsync<List<OfflineProductDto>>("api/sales/offline-catalog");
                if (products == null) return false;
                await _store.ReplaceProductsAsync(products);
                return true;
            }
            catch
            {
                // No connection (or the request failed) — the previously cached catalog, if any,
                // is left in place untouched.
                return false;
            }
        }

        /// <returns>How many queued sales were successfully synced.</returns>
        public async Task<int> PushPendingSalesAsync()
        {
            var pending = await _store.GetPendingSalesAsync();
            var synced = 0;
            foreach (var sale in pending)
            {
                try
                {
                    var response = await _http.PostAsJsonAsync("api/sales/offline-sync", sale);
                    if (!response.IsSuccessStatusCode) continue;
                    await _store.RemovePendingSaleAsync(sale.SyncId);
                    synced++;
                }
                catch
                {
                    // Still offline (or the server rejected it) — leave it queued and try again
                    // on the next sync pass.
                    break;
                }
            }
            return synced;
        }
    }
}
