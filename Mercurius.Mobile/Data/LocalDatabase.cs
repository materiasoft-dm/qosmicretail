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
                conn.InsertOrReplace(product);
            }
        });
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

    // Wipes all locally cached data — used on logout so the next login starts from a clean slate
    // rather than showing a previous user's cached catalog.
    public async Task ClearAllAsync()
    {
        await EnsureInitializedAsync();
        await _connection.DeleteAllAsync<LocalProduct>();
        await _connection.DeleteAllAsync<SyncState>();
    }
}
