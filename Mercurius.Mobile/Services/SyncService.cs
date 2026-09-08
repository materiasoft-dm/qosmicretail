using Mercurius.Mobile.Data;

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
}
