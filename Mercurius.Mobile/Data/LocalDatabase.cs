using System.Security.Cryptography;
using System.Text;
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
            await _connection.CreateTableAsync<LocalTerminal>();
            await _connection.CreateTableAsync<LocalZReading>();
            await _connection.CreateTableAsync<LocalDiscountType>();
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
            conn.Execute($"DELETE FROM SaleItems WHERE SaleSyncId IN (SELECT SyncId FROM Sales WHERE IsSynced = 1 AND NOT {AwaitingZReadingSql})");
            conn.Execute($"DELETE FROM Sales WHERE IsSynced = 1 AND NOT {AwaitingZReadingSql}");
            conn.Execute("DELETE FROM Products WHERE IsPendingSync = 0");
            conn.DeleteAll<SyncState>();
            // The next account may be another pharmacy's, with its own discounts.
            conn.DeleteAll<LocalDiscountType>();
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
                    // Local-only, and what this terminal printed: a server without the BIR fields
                    // (or a copy missing them) must not erase the e-journal or reopen a Z-reading.
                    sale.ZReadingCounter = existing.ZReadingCounter;
                    if (existing.InvoiceSequence > 0 && sale.InvoiceSequence == 0) CopyBirFields(existing, sale);
                    if (string.IsNullOrEmpty(sale.ReceiptText))
                    {
                        sale.ReceiptText = existing.ReceiptText;
                        sale.JournalHash = existing.JournalHash;
                    }
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

    // This terminal's own invoices that no Z-reading has closed yet. They're needed for the next
    // Z-reading, so logout and pruning leave them alone even once they're synced.
    private const string AwaitingZReadingSql = "(IsFromThisDevice = 1 AND InvoiceSequence > 0 AND ZReadingCounter IS NULL)";

    // Keeps the local history bounded to the retention window. Never touches unsynced sales —
    // those are the only copy anywhere, however old — or sales awaiting a Z-reading.
    public async Task<int> PruneSyncedSalesBeforeAsync(DateTime cutoffUtc)
    {
        await EnsureInitializedAsync();
        var removed = 0;
        await _connection.RunInTransactionAsync(conn =>
        {
            conn.Execute($"DELETE FROM SaleItems WHERE SaleSyncId IN (SELECT SyncId FROM Sales WHERE IsSynced = 1 AND InvoiceDate < ? AND NOT {AwaitingZReadingSql})", cutoffUtc);
            removed = conn.Execute($"DELETE FROM Sales WHERE IsSynced = 1 AND InvoiceDate < ? AND NOT {AwaitingZReadingSql}", cutoffUtc);
        });
        return removed;
    }

    private static void CopyBirFields(LocalSale from, LocalSale to)
    {
        to.PosTerminalId = from.PosTerminalId;
        to.InvoiceSequence = from.InvoiceSequence;
        to.GrossAmount = from.GrossAmount;
        to.VatableSales = from.VatableSales;
        to.VatAmount = from.VatAmount;
        to.VatExemptSales = from.VatExemptSales;
        to.ZeroRatedSales = from.ZeroRatedSales;
        to.LessVat = from.LessVat;
        to.DiscountAmount = from.DiscountAmount;
        to.DiscountTypeId = from.DiscountTypeId;
        to.DiscountName = from.DiscountName;
        to.SpecialDiscountType = from.SpecialDiscountType;
        to.SpecialDiscountIdNumber = from.SpecialDiscountIdNumber;
        to.SpecialDiscountHolderName = from.SpecialDiscountHolderName;
        to.SpecialDiscountHolderTin = from.SpecialDiscountHolderTin;
        to.BuyerName = from.BuyerName;
        to.BuyerTin = from.BuyerTin;
        to.BuyerAddress = from.BuyerAddress;
        to.BuyerBusinessStyle = from.BuyerBusinessStyle;
    }

    // Records a completed sale immediately — this is what makes checkout work with zero
    // connectivity. Also decrements the local product cache's stock right away (optimistic;
    // the server recomputes the authoritative figure from FIFO batches once this sale syncs)
    // so the POS grid reflects the sale instantly instead of waiting on a round trip.
    //
    // In the same transaction it issues the BIR invoice: takes this terminal's next serial number,
    // renders the receipt with it (renderReceipt), and chains the receipt into the e-journal by
    // hash. Doing all of it at once means a serial is never skipped or used twice, even if the
    // app dies mid-checkout.
    public async Task SaveSaleAsync(LocalSale sale, List<LocalSaleItem> items, Func<LocalSale, LocalTerminal?, string> renderReceipt)
    {
        await EnsureInitializedAsync();
        await _connection.RunInTransactionAsync(conn =>
        {
            var terminal = conn.Find<LocalTerminal>(LocalTerminal.SingletonId);
            if (terminal is { TerminalId: > 0 })
            {
                var sequence = terminal.LastInvoiceSequence + 1;
                sale.PosTerminalId = terminal.TerminalId;
                sale.InvoiceSequence = sequence;
                sale.InvoiceNumber = $"{terminal.TerminalCode}-{Mercurius.Shared.Bir.BirFormat.InvoiceNumber(sequence)}";
                terminal.LastInvoiceSequence = sequence;
            }

            sale.ReceiptText = renderReceipt(sale, terminal);
            sale.JournalHash = JournalHash(terminal?.LastJournalHash, sale.ReceiptText);
            if (terminal != null)
            {
                terminal.LastJournalHash = sale.JournalHash;
                conn.Update(terminal);
            }

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

    // ---------------------------------------------------------------- BIR terminal, readings, e-journal

    // SHA-256 of the previous invoice's hash and this invoice's text: changing or removing any
    // earlier invoice breaks every hash after it.
    public static string JournalHash(string? previousHash, string receiptText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes((previousHash ?? "") + "\n" + receiptText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task ReplaceDiscountTypesAsync(IReadOnlyList<LocalDiscountType> discountTypes)
    {
        await EnsureInitializedAsync();
        await _connection.RunInTransactionAsync(conn =>
        {
            conn.DeleteAll<LocalDiscountType>();
            conn.InsertAll(discountTypes);
        });
    }

    public async Task<List<LocalDiscountType>> GetDiscountTypesAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<LocalDiscountType>().OrderBy(d => d.SortOrder).ToListAsync();
    }

    public async Task<LocalTerminal?> GetTerminalAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.FindAsync<LocalTerminal>(LocalTerminal.SingletonId);
    }

    // Merges the server's view of this terminal into the local row (see BirService for the rules).
    public async Task<LocalTerminal> UpdateTerminalAsync(Func<LocalTerminal?, LocalTerminal> merge)
    {
        await EnsureInitializedAsync();
        LocalTerminal result = null!;
        await _connection.RunInTransactionAsync(conn =>
        {
            result = merge(conn.Find<LocalTerminal>(LocalTerminal.SingletonId));
            result.Id = LocalTerminal.SingletonId;
            conn.InsertOrReplace(result);
        });
        return result;
    }

    // This terminal's invoices since its last Z-reading, oldest first (an X-reading's contents).
    public async Task<List<LocalSale>> GetSalesAwaitingZReadingAsync(int terminalId)
    {
        await EnsureInitializedAsync();
        var sales = await _connection.Table<LocalSale>()
            .Where(s => s.IsFromThisDevice && s.PosTerminalId == terminalId && s.ZReadingCounter == null)
            .ToListAsync();
        return sales.Where(s => s.InvoiceSequence > 0).OrderBy(s => s.InvoiceSequence).ToList();
    }

    // Takes a Z-reading atomically: the sales it covers, the reading built from them, the sales
    // marked as closed by it, and the terminal's Z counter and grand total moved forward. A sale
    // rung up a moment later can't fall between the reading and the marking.
    public async Task<LocalZReading?> TakeZReadingAsync(Func<LocalTerminal, List<LocalSale>, LocalZReading> build)
    {
        await EnsureInitializedAsync();
        LocalZReading? reading = null;
        await _connection.RunInTransactionAsync(conn =>
        {
            var terminal = conn.Find<LocalTerminal>(LocalTerminal.SingletonId);
            if (terminal is not { TerminalId: > 0 }) return;

            var terminalId = terminal.TerminalId;
            var sales = conn.Table<LocalSale>()
                .Where(s => s.IsFromThisDevice && s.PosTerminalId == terminalId && s.ZReadingCounter == null)
                .ToList()
                .Where(s => s.InvoiceSequence > 0)
                .OrderBy(s => s.InvoiceSequence)
                .ToList();

            reading = build(terminal, sales);
            conn.Insert(reading);
            foreach (var sale in sales)
            {
                sale.ZReadingCounter = reading.ZCounter;
                conn.Update(sale);
            }
            terminal.LastZCounter = reading.ZCounter;
            terminal.AccumulatedGrandTotal = reading.NewGrandTotal;
            terminal.LastInvoiceSequence = Math.Max(terminal.LastInvoiceSequence, reading.EndingInvoiceSequence);
            conn.Update(terminal);
        });
        return reading;
    }

    public async Task<List<LocalZReading>> GetZReadingsAsync()
    {
        await EnsureInitializedAsync();
        var readings = await _connection.Table<LocalZReading>().ToListAsync();
        return readings.OrderByDescending(z => z.ZCounter).ThenByDescending(z => z.ReadingUtc).ToList();
    }

    public async Task<List<LocalZReading>> GetUnsyncedZReadingsAsync()
    {
        await EnsureInitializedAsync();
        return await _connection.Table<LocalZReading>().Where(z => !z.IsSynced).ToListAsync();
    }

    public async Task MarkZReadingSyncedAsync(Guid syncId)
    {
        await EnsureInitializedAsync();
        await _connection.ExecuteAsync("UPDATE ZReadings SET IsSynced = 1 WHERE SyncId = ?", syncId);
    }

    // Every invoice this terminal issued that's still on the device, in serial order — the
    // e-journal export. (The server keeps every synced invoice's text too, for the long term.)
    public async Task<List<LocalSale>> GetJournalAsync(int terminalId)
    {
        await EnsureInitializedAsync();
        var sales = await _connection.Table<LocalSale>()
            .Where(s => s.IsFromThisDevice && s.PosTerminalId == terminalId)
            .ToListAsync();
        return sales.Where(s => !string.IsNullOrEmpty(s.ReceiptText)).OrderBy(s => s.InvoiceSequence).ToList();
    }
}
