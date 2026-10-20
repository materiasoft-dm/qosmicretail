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

    // Pushes products created on this device that the server hasn't confirmed yet. Like sales, a
    // product is only flipped to synced once the server reports success for its SyncId, so a
    // failure just leaves it queued. Returns how many were confirmed; the server's reason for each
    // one it turned down (e.g. no permission) is added to rejections.
    public async Task<int> PushProductsAsync(CancellationToken ct = default, ICollection<string>? rejections = null)
    {
        var pending = await _localDatabase.GetUnsyncedProductsAsync();
        if (pending.Count == 0) return 0;

        var dtos = pending.Select(p => new ProductPushDto
        {
            SyncId = p.SyncId,
            ProductCode = p.ProductCode,
            // "" rather than null when there's none: the server reads null as "this app doesn't
            // know about barcodes, keep the existing one" and "" as "cleared on purpose".
            Barcode = p.Barcode ?? "",
            Name = p.Name,
            Description = p.Description,
            CategoryName = p.CategoryName,
            CurrentCostPrice = p.CurrentCostPrice,
            CurrentSalePrice = p.CurrentSalePrice,
            // Only used by the server when this push creates the product; ignored on updates.
            CurrentStock = p.CurrentStock,
            LowStockCount = p.LowStockCount,
            MarkUpPercentage = p.MarkUpPercentage,
            IsActive = p.IsActive,
            VatType = p.VatType
        }).ToList();

        var results = await _syncApiService.PushProductsAsync(dtos, ct);
        var syncedCount = 0;
        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result.Error))
            {
                await _localDatabase.MarkProductSyncedAsync(result.SyncId);
                syncedCount++;
            }
            else
            {
                AddRejection(rejections, result.Error);
            }
        }
        return syncedCount;
    }

    public async Task<int> SyncProductsAsync(CancellationToken ct = default)
    {
        // Push first so a locally-created product reaches the server before the pull, rather than
        // the pull racing it. A failed push must not block the pull (or vice versa) — the product
        // simply stays queued.
        try { await PushProductsAsync(ct); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Product push failed: {ex.Message}"); }

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
            Barcode = p.Barcode,
            Name = p.Name,
            Description = p.Description,
            CategoryName = p.CategoryName,
            CurrentCostPrice = p.CurrentCostPrice,
            CurrentSalePrice = p.CurrentSalePrice,
            CurrentStock = p.CurrentStock,
            LowStockCount = p.LowStockCount,
            MarkUpPercentage = p.MarkUpPercentage,
            IsActive = p.IsActive,
            VatType = p.VatType,
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
    public async Task<int> SyncSalesAsync(CancellationToken ct = default, ICollection<string>? rejections = null)
    {
        var pending = await _localDatabase.GetUnsyncedSalesAsync();
        if (pending.Count == 0) return 0;

        // A sale can include a product created on this device (e.g. scanned, not found, added on
        // the spot) — the server can only record that line once it knows the product, so push
        // any pending products first. If this fails, the sale push below fails for that line and
        // the sale simply stays queued for the next attempt.
        try { await PushProductsAsync(ct); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Product push before sales failed: {ex.Message}"); }

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
                PaymentMethod = sale.PaymentMethod,
                PaidAmount = sale.PaidAmount,
                CashierUserId = sale.CashierUserId,
                PosTerminalId = sale.InvoiceSequence > 0 ? sale.PosTerminalId : null,
                TerminalInvoiceSequence = sale.InvoiceSequence > 0 ? sale.InvoiceSequence : null,
                VatableSales = sale.VatableSales,
                VatAmount = sale.VatAmount,
                VatExemptSales = sale.VatExemptSales,
                ZeroRatedSales = sale.ZeroRatedSales,
                LessVat = sale.LessVat,
                DiscountAmount = sale.DiscountAmount,
                DiscountTypeId = sale.DiscountTypeId,
                DiscountName = sale.DiscountName,
                SpecialDiscountType = sale.SpecialDiscountType,
                SpecialDiscountIdNumber = sale.SpecialDiscountIdNumber,
                SpecialDiscountHolderName = sale.SpecialDiscountHolderName,
                SpecialDiscountHolderTin = sale.SpecialDiscountHolderTin,
                BuyerName = sale.BuyerName,
                BuyerTin = sale.BuyerTin,
                BuyerAddress = sale.BuyerAddress,
                BuyerBusinessStyle = sale.BuyerBusinessStyle,
                ReceiptText = sale.ReceiptText,
                JournalHash = sale.JournalHash,
                Items = items.Select(i => new SaleItemPushDto
                {
                    SyncId = i.SyncId,
                    ProductSyncId = i.ProductSyncId,
                    Quantity = i.Quantity,
                    SalePrice = i.SalePrice,
                    CostPrice = i.CostPrice,
                    VatType = i.VatType,
                    VatAmount = i.VatAmount,
                    DiscountAmount = i.DiscountAmount,
                    NetAmount = i.EffectiveLineTotal
                }).ToList()
            });
        }

        var results = await _syncApiService.PushSalesAsync(dtos, ct);
        var syncedCount = 0;
        // A failed sale is NOT marked synced — it stays queued and retries next cycle. The server
        // rolls each failed sale back completely, so a retry never double-counts anything.
        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result.Error))
            {
                await _localDatabase.MarkSaleSyncedAsync(result.SyncId);
                syncedCount++;
            }
            else
            {
                AddRejection(rejections, result.Error);
            }
        }
        return syncedCount;
    }

    // One line per distinct reason, so fifty queued sales refused for the same reason read as one.
    private static void AddRejection(ICollection<string>? rejections, string? error)
    {
        if (rejections == null || string.IsNullOrWhiteSpace(error) || rejections.Contains(error)) return;
        rejections.Add(error);
    }

    // ---------------------------------------------------------------- receipts (all sales)

    private const string ReceiptsEntityName = "receipts";
    private const int ReceiptPageSize = 100;
    // Bounds one sync pass so a huge backlog can't hog the connection; the cursor persists, so
    // the next pass simply continues where this one stopped.
    private const int MaxReceiptPagesPerPass = 20;

    // How far back this device keeps sales history (its own synced sales included). Older synced
    // rows are pruned; unsynced ones never are.
    public static readonly TimeSpan ReceiptRetention = TimeSpan.FromDays(30);

    // Pulls every sale in the tenant (all terminals + web) changed since the last pull, within
    // the retention window. Resumable: the cursor is saved after EVERY page, so an interrupted
    // pull (app killed, connection dropped) loses at most one page of work, never skips data.
    public async Task<int> PullReceiptsAsync(CancellationToken ct = default)
    {
        var fromDate = DateTime.UtcNow - ReceiptRetention;
        var cursor = await _localDatabase.GetCursorAsync(ReceiptsEntityName);
        var total = 0;

        for (var pageNo = 0; pageNo < MaxReceiptPagesPerPass; pageNo++)
        {
            var page = await _syncApiService.PullReceiptsAsync(cursor, fromDate, ReceiptPageSize, ct);
            if (page.Items.Count > 0)
            {
                await _localDatabase.UpsertPulledSalesAsync(page.Items.Select(ToLocal).ToList());
                total += page.Items.Count;
            }

            if (!string.IsNullOrEmpty(page.NextCursor) && page.NextCursor != cursor)
            {
                cursor = page.NextCursor;
                await _localDatabase.SetCursorAsync(ReceiptsEntityName, cursor);
            }

            if (!page.HasMore) break;
        }

        await _localDatabase.PruneSyncedSalesBeforeAsync(fromDate);
        return total;
    }

    private static (LocalSale, List<LocalSaleItem>) ToLocal(ReceiptDto r)
    {
        var sale = new LocalSale
        {
            SyncId = r.SyncId,
            InvoiceDate = r.InvoiceDate,
            LocationId = r.LocationId,
            Notes = r.Notes,
            PaymentMethod = r.PaymentMethod,
            PaidAmount = r.PaidAmount,
            GrandTotal = r.GrandTotal,
            IsSynced = true,
            CreatedUtc = r.InvoiceDate,
            InvoiceNumber = r.InvoiceNumber,
            LocationName = r.LocationName,
            CashierName = r.CashierName,
            SourceChannel = r.SourceChannel,
            SourceDeviceId = r.SourceDeviceId,
            SourceDeviceName = r.SourceDeviceName,
            Status = r.Status,
            HasRefund = r.HasRefund,
            RefundedAmount = r.RefundedAmount,
            ServerModifiedUtc = r.LastModifiedUtc,
            PosTerminalId = r.PosTerminalId,
            InvoiceSequence = r.TerminalInvoiceSequence ?? 0,
            GrossAmount = r.Items.Sum(i => Mercurius.Shared.Bir.BirTaxCalculator.Round(i.Quantity * i.SalePrice)),
            VatableSales = r.VatableSales,
            VatAmount = r.VatAmount,
            VatExemptSales = r.VatExemptSales,
            ZeroRatedSales = r.ZeroRatedSales,
            LessVat = r.LessVat,
            DiscountAmount = r.DiscountAmount,
            DiscountTypeId = r.DiscountTypeId,
            DiscountName = r.DiscountName,
            SpecialDiscountType = r.SpecialDiscountType,
            SpecialDiscountIdNumber = r.SpecialDiscountIdNumber,
            SpecialDiscountHolderName = r.SpecialDiscountHolderName,
            SpecialDiscountHolderTin = r.SpecialDiscountHolderTin,
            BuyerName = r.BuyerName,
            BuyerTin = r.BuyerTin,
            BuyerAddress = r.BuyerAddress,
            BuyerBusinessStyle = r.BuyerBusinessStyle,
            ReceiptText = r.ReceiptText,
            JournalHash = r.JournalHash
        };
        var items = r.Items.Select(i => new LocalSaleItem
        {
            SyncId = i.SyncId,
            SaleSyncId = r.SyncId,
            ProductSyncId = i.ProductSyncId,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            SalePrice = i.SalePrice,
            LineTotal = i.LineTotal,
            RefundedQuantity = i.RefundedQuantity,
            VatType = i.VatType,
            VatAmount = i.VatAmount,
            DiscountAmount = i.DiscountAmount
        }).ToList();
        return (sale, items);
    }
}
