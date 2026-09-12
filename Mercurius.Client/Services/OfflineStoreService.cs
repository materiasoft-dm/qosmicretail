using Mercurius.Shared.Sales;
using Microsoft.JSInterop;

namespace Mercurius.Client.Services
{
    // Thin wrapper over wwwroot/js/offlineStore.js's IndexedDB-backed product cache and
    // pending-sale queue. See OfflineSyncService for when these get populated/drained.
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

        public Task QueueSaleAsync(OfflineSaleRequest sale) =>
            _js.InvokeVoidAsync("mercuriusOfflineStore.queueSale", sale).AsTask();

        public async Task<List<OfflineSaleRequest>> GetPendingSalesAsync() =>
            await _js.InvokeAsync<List<OfflineSaleRequest>>("mercuriusOfflineStore.getPendingSales");

        public Task RemovePendingSaleAsync(Guid syncId) =>
            _js.InvokeVoidAsync("mercuriusOfflineStore.removePendingSale", syncId).AsTask();
    }
}
