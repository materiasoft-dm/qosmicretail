using SQLite;

namespace Mercurius.Mobile.Data;

// The app's local SQLite store — every screen reads from here, never directly from the network.
// SyncService is the only thing that writes to it based on what the server API returns.
public class LocalDatabase
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public LocalDatabase()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "mercurius_mobile.db3");
        _connection = new SQLiteAsyncConnection(dbPath);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await _connection.CreateTableAsync<LocalProduct>();
            await _connection.CreateTableAsync<SyncState>();
            await _connection.CreateTableAsync<LocalSale>();
            await _connection.CreateTableAsync<LocalSaleItem>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<LocalProduct>> GetProductsAsync(string? searchText = null)
    {
        await EnsureInitializedAsync();
        var query = _connection.Table<LocalProduct>().Where(p => p.IsActive);
        var all = await query.ToListAsync();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            all = all.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.ProductCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.Barcode?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.CategoryName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        return all.OrderBy(p => p.Name).ToList();
    }

    public async Task UpsertProductsAsync(IEnumerable<LocalProduct> products)
    {
        await EnsureInitializedAsync();
        var list = products.ToList();
        await _connection.RunInTransactionAsync(conn =>
        {
            foreach (var product in list)
            {
                // A product created on this device and not yet confirmed by the server is the
                // source of truth until its push succeeds — never overwrite it from a pull.
                var existing = conn.Find<LocalProduct>(product.SyncId);
                if (existing?.IsPendingSync == true) continue;

                conn.InsertOrReplace(product);
            }
        });
    }

    // Saves a product created on this device. It's immediately visible to every screen (Products,
    // Sell) and is queued for the next push via IsPendingSync.
    public async Task SaveNewProductAsync(LocalProduct product)
    {
        await EnsureInitializedAsync();
        product.IsPendingSync = true;
        await _connection.InsertAsync(product);
    }

    // Saves an edit made on this device and queues it for push. Stock is preserved from the
    // stored row — the edit form can't change it, and the server ignores stock on updates anyway.
    public async Task SaveEditedProductAsync(LocalProduct product)
    {
        await EnsureInitializedAsync();
        var existing = await _connection.FindAsync<LocalProduct>(product.SyncId);
        if (existing != null) product.CurrentStock = existing.CurrentStock;
        product.IsPendingSync = true;
        await _connection.InsertOrReplaceAsync(product);
    }

    public async Task<List<LocalProduct>> GetUnsyncedProductsAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<LocalProduct>().Where(p => p.IsPendingSync).ToListAsync();
    }

    public async Task MarkProductSyncedAsync(Guid syncId)
    {
        await EnsureInitializedAsync();
        var product = await _connection.Table<LocalProduct>().Where(p => p.SyncId == syncId).FirstOrDefaultAsync();
        if (product == null) return;
        product.IsPendingSync = false;
        await _connection.UpdateAsync(product);
    }

    // Includes inactive products — a code is "taken" whether or not the product is currently sellable.
    public async Task<bool> ProductCodeExistsAsync(string productCode)
    {
        await EnsureInitializedAsync();
        var all = await _connection.Table<LocalProduct>().ToListAsync();
        return all.Any(p => string.Equals(p.ProductCode, productCode, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DateTime?> GetLastSyncedAsync(string entityName)
    {
        await EnsureInitializedAsync();
        var state = await _connection.Table<SyncState>()
            .Where(s => s.EntityName == entityName)
            .FirstOrDefaultAsync();
        return state?.LastSyncedUtc;
    }

    public async Task SetLastSyncedAsync(string entityName, DateTime utc)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(new SyncState { EntityName = entityName, LastSyncedUtc = utc });
    }

    // Wipes locally cached data on logout so the next login starts from a clean slate — EXCEPT
    // anything that exists nowhere else yet: sales and products created here that haven't reached
    // the server. Those used to be deleted too, i.e. a logout while offline silently destroyed
    // real sales. AppShell now refuses a normal logout while any are pending; this is the second
    // line of defence for the "log out anyway" path, where they stay queued for the next sign-in.
    public async Task ClearAllAsync()
    {
        await EnsureInitializedAsync();
        await _connection.RunInTransactionAsync(conn =>
        {
            conn.Execute("DELETE FROM SaleItems WHERE SaleSyncId IN (SELECT SyncId FROM Sales WHERE IsSynced = 1)");
            conn.Execute("DELETE FROM Sales WHERE IsSynced = 1");
            conn.Execute("DELETE FROM Products WHERE IsPendingSync = 0");
            conn.DeleteAll<SyncState>();
        });
    }

    public async Task<(int Sales, int Products)> GetPendingUploadCountsAsync()
    {
        await EnsureInitializedAsync();
        var sales = await _connection.Table<LocalSale>().Where(s => !s.IsSynced).CountAsync();
        var products = await _connection.Table<LocalProduct>().Where(p => p.IsPendingSync).CountAsync();
        return (sales, products);
    }

    public async Task<string?> GetCursorAsync(string entityName)
    {
        await EnsureInitializedAsync();
        var state = await _connection.Table<SyncState>().Where(s => s.EntityName == entityName).FirstOrDefaultAsync();
        return state?.Cursor;
    }

    public async Task SetCursorAsync(string entityName, string? cursor)
    {
        await EnsureInitializedAsync();
        await _connection.InsertOrReplaceAsync(new SyncState { EntityName = entityName, LastSyncedUtc = DateTime.UtcNow, Cursor = cursor });
    }

    // Applies one page of pulled receipts. The server is the authority for every row it sends,
    // so each one replaces the local copy (and its lines) wholesale. A pulled copy of one of our
    // OWN sales is proof the server has it — which is exactly how a push whose response was lost
    // gets marked synced instead of being re-sent forever.
    public async Task UpsertPulledSalesAsync(IReadOnlyList<(LocalSale Sale, List<LocalSaleItem> Items)> pulled)
    {
        await EnsureInitializedAsync();
        await _connection.RunInTransactionAsync(conn =>
        {
            foreach (var (sale, items) in pulled)
            {
                var existing = conn.Find<LocalSale>(sale.SyncId);
                if (existing != null)
                {
                    sale.IsFromThisDevice = existing.IsFromThisDevice;
                    sale.CashierUserId ??= existing.CashierUserId;
                    sale.CreatedUtc = existing.CreatedUtc;
                }
                sale.IsSynced = true;
                conn.InsertOrReplace(sale);

                conn.Execute("DELETE FROM SaleItems WHERE SaleSyncId = ?", sale.SyncId);
                foreach (var item in items)
                {
                    conn.InsertOrReplace(item);
                }
                // Stock deliberately untouched: the product pull carries the server's figure,
                // which already includes this sale.
            }
        });
    }

    // Keeps the local history bounded to the retention window. Never touches unsynced sales —
    // those are the only copy anywhere, however old.
    public async Task<int> PruneSyncedSalesBeforeAsync(DateTime cutoffUtc)
    {
        await EnsureInitializedAsync();
        var removed = 0;
        await _connection.RunInTransactionAsync(conn =>
        {
            conn.Execute("DELETE FROM SaleItems WHERE SaleSyncId IN (SELECT SyncId FROM Sales WHERE IsSynced = 1 AND InvoiceDate < ?)", cutoffUtc);
            removed = conn.Execute("DELETE FROM Sales WHERE IsSynced = 1 AND InvoiceDate < ?", cutoffUtc);
        });
        return removed;
    }

    // Records a completed sale immediately — this is what makes checkout work with zero
    // connectivity. Also decrements the local product cache's stock right away (optimistic;
    // the server recomputes the authoritative figure from FIFO batches once this sale syncs)
    // so the POS grid reflects the sale instantly instead of waiting on a round trip.
    public async Task SaveSaleAsync(LocalSale sale, List<LocalSaleItem> items)
    {
        await EnsureInitializedAsync();
        await _connection.RunInTransactionAsync(conn =>
        {
            conn.Insert(sale);
            foreach (var item in items)
            {
                conn.Insert(item);

                var product = conn.Table<LocalProduct>().FirstOrDefault(p => p.SyncId == item.ProductSyncId);
                if (product != null)
                {
                    product.CurrentStock -= item.Quantity;
                    conn.Update(product);
                }
            }
        });
    }

    public async Task<List<LocalSale>> GetUnsyncedSalesAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<LocalSale>().Where(s => !s.IsSynced).ToListAsync();
    }

    public async Task<List<LocalSale>> GetAllSalesAsync()
    {
        await EnsureInitializedAsync();
        var sales = await _connection.Table<LocalSale>().ToListAsync();
        return sales.OrderByDescending(s => s.InvoiceDate).ToList();
    }

    public async Task<List<LocalSaleItem>> GetAllSaleItemsAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<LocalSaleItem>().ToListAsync();
    }

    public async Task<List<LocalSaleItem>> GetSaleItemsAsync(Guid saleSyncId)
    {
        await EnsureInitializedAsync();
        return await _connection.Table<LocalSaleItem>().Where(i => i.SaleSyncId == saleSyncId).ToListAsync();
    }

    public async Task MarkSaleSyncedAsync(Guid saleSyncId)
    {
        await EnsureInitializedAsync();
        var sale = await _connection.Table<LocalSale>().Where(s => s.SyncId == saleSyncId).FirstOrDefaultAsync();
        if (sale == null) return;
        sale.IsSynced = true;
        await _connection.UpdateAsync(sale);
    }

    public async Task<int> GetPendingSaleCountAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<LocalSale>().Where(s => !s.IsSynced).CountAsync();
    }
}
