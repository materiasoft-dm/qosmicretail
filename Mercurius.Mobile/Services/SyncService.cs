using Mercurius.Mobile.Data;
using Mercurius.Mobile.Models;

namespace Mercurius.Mobile.Services;

// Refreshes the local database from the server. Every screen reads from LocalDatabase; this is
// the only thing that ever writes to it from network data. The pull cursor is the max
// LastModifiedUtc actually seen in a response, not the device's own clock at request time — this
// stays correct even if the device's clock is off, since it only ever trusts timestamps the
// server itself produced.
public class SyncService
{
    private const string ProductsEntityName = "products";

    private readonly SyncApiService _syncApiService;
    private readonly LocalDatabase _localDatabase;

    public SyncService(SyncApiService syncApiService, LocalDatabase localDatabase)
    {
        _syncApiService = syncApiService;
        _localDatabase = localDatabase;
    }

    public async Task<int> SyncProductsAsync(CancellationToken ct = default)
    {
        var since = await _localDatabase.GetLastSyncedAsync(ProductsEntityName) ?? DateTime.MinValue;
        var changed = await _syncApiService.PullProductsAsync(since, ct);

        if (changed.Count == 0)
        {
            return 0;
        }

        var localRows = changed.Select(p => new LocalProduct
        {
            SyncId = p.SyncId,
            ProductCode = p.ProductCode,
            Name = p.Name,
            Description = p.Description,
            CategoryName = p.CategoryName,
            CurrentCostPrice = p.CurrentCostPrice,
            CurrentSalePrice = p.CurrentSalePrice,
            CurrentStock = p.CurrentStock,
            LowStockCount = p.LowStockCount,
            IsActive = p.IsActive,
            LastModifiedUtc = p.LastModifiedUtc
        });

        await _localDatabase.UpsertProductsAsync(localRows);

        var newCursor = changed.Max(p => p.LastModifiedUtc);
        await _localDatabase.SetLastSyncedAsync(ProductsEntityName, newCursor);

        return changed.Count;
    }

    // Pushes every sale still sitting in the local queue (created while offline, or simply not
    // yet synced) to the server. Each sale is idempotent by SyncId on the server side, so a sale
    // that partially succeeds — or gets retried after a dropped connection — never double-charges
    // inventory; we only flip a sale's local IsSynced flag once the server confirms it, so a
    // failed push just leaves it queued for the next attempt.
    public async Task<int> SyncSalesAsync(CancellationToken ct = default)
    {
        var pending = await _localDatabase.GetUnsyncedSalesAsync();
        if (pending.Count == 0) return 0;

        var dtos = new List<SalePushDto>();
        foreach (var sale in pending)
        {
            var items = await _localDatabase.GetSaleItemsAsync(sale.SyncId);
            dtos.Add(new SalePushDto
            {
                SyncId = sale.SyncId,
                InvoiceDate = sale.InvoiceDate,
                CustomerId = sale.CustomerId,
                LocationId = sale.LocationId,
                Notes = sale.Notes,
                PaidAmount = sale.PaidAmount,
                Items = items.Select(i => new SaleItemPushDto
                {
                    SyncId = i.SyncId,
                    ProductSyncId = i.ProductSyncId,
                    Quantity = i.Quantity,
                    SalePrice = i.SalePrice,
                    CostPrice = i.CostPrice
                }).ToList()
            });
        }

        var results = await _syncApiService.PushSalesAsync(dtos, ct);
        var syncedCount = 0;
        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result.Error))
            {
                await _localDatabase.MarkSaleSyncedAsync(result.SyncId);
                syncedCount++;
            }
        }
        return syncedCount;
    }
}
