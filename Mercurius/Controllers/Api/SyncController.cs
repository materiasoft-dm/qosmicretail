using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Models.Sync;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercurius.Controllers.Api
{
    // Pull/push sync for the offline-first MAUI mobile app. Devices never share the server's int
    // Id — every record is identified across devices by its SyncId (Guid), generated wherever the
    // record was first created (client or server).
    //
    // Conflict handling is last-write-wins, but the "write" timestamp is always the server's own
    // clock (UpdatedDate/CreateDate), stamped at the moment a write is accepted here or from the
    // admin web UI — never a client-supplied timestamp. This sidesteps device clock-skew entirely:
    // "last write" simply means "the last write this server accepted," which is well-defined
    // regardless of how many devices are pushing concurrently. Business-meaning dates the client
    // actually observed firsthand (e.g. InvoiceDate) are still trusted from the client — only the
    // technical bookkeeping fields used for conflict resolution are server-stamped.
    //
    // Products fully support create-or-update via push. Invoices are create-once (idempotent by
    // SyncId): a synced sale is a completed financial record, and blindly overwriting one via sync
    // isn't safe — edits to a submitted sale should go through a proper refund/void flow instead.
    [ApiController]
    [Route("api/sync")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SyncController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.SaleRecorder _saleRecorder;
        private readonly Mercurius.Services.UserPermissions _userPermissions;
        private HashSet<string>? _permissions;

        public SyncController(IUnitOfWork unitOfWork, Mercurius.Services.SaleRecorder saleRecorder, Mercurius.Services.UserPermissions userPermissions)
        {
            _unitOfWork = unitOfWork;
            _saleRecorder = saleRecorder;
            _userPermissions = userPermissions;
        }

        private Guid CurrentUserId =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;

        // Pushes are checked against the same permissions as the web pages that do the same thing
        // (selling, creating/editing products). A refusal comes back per item, like any other
        // push error, so the device keeps the record queued: nothing is lost, and it goes up once
        // an admin grants the permission or someone who has it signs in on that device.
        private async Task<bool> CanAsync(string permission)
        {
            _permissions ??= await _userPermissions.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));
            return _permissions.Contains(permission);
        }

        // ============================================
        // LOCATIONS (which clinic/branch this device is assigned to)
        // ============================================
        // Backs Mercurius.Mobile's Settings page — a device-level assignment, deliberately
        // separate from UserCurrentLocation (which is per-*user*, driving the web app's location
        // switcher). A shared POS terminal has a fixed physical clinic regardless of who's signed
        // in on it, so this can't just reuse that mechanism. No extra page-claim required beyond
        // the JWT itself — mobile tokens don't carry access.pages claims at all yet (a pre-existing
        // gap, see MULTITENANCY_ARCHITECTURE.md §2.3), so gating this on one wasn't an option.

        [HttpGet("locations")]
        public IActionResult GetLocations(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var locations = _unitOfWork.Query<Location>()
                .Where(l => l.IsActive)
                .OrderBy(l => l.Name)
                .Select(l => new LocationSyncDto { Id = l.Id, Name = l.Name })
                .ToList();
            return Ok(locations);
        }

        // ============================================
        // PRODUCTS
        // ============================================

        [HttpGet("products/pull")]
        public IActionResult PullProducts([FromQuery] DateTime? since, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var sinceValue = since ?? DateTime.MinValue;

            // >= (not >) so a record modified at exactly `since` is returned again rather than
            // silently skipped — harmless, since the client upserts by SyncId anyway.
            var products = _unitOfWork.Query<Product>()
                .Where(p => (p.UpdatedDate ?? p.CreateDate) >= sinceValue)
                .OrderBy(p => p.UpdatedDate ?? p.CreateDate)
                .Select(p => new ProductSyncDto
                {
                    SyncId = p.SyncId,
                    ProductCode = p.ProductCode,
                    Barcode = p.Barcode,
                    Name = p.Name,
                    Description = p.Description,
                    CategoryName = p.ProductCategory != null ? p.ProductCategory.Name : null,
                    CurrentCostPrice = p.CurrentCostPrice,
                    CurrentSalePrice = p.CurrentSalePrice,
                    CurrentStock = p.CurrentStock,
                    LowStockCount = p.LowStockCount,
                    MarkUpPercentage = p.MarkUpPercentage,
                    IsActive = p.IsActive,
                    LastModifiedUtc = p.UpdatedDate ?? p.CreateDate
                })
                .ToList();

            return Ok(products);
        }

        [HttpPost("products/push")]
        public async Task<IActionResult> PushProducts([FromBody] List<ProductPushDto> items, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var results = new List<SyncPushResultItem>();
            var now = DateTime.UtcNow;

            foreach (var item in items ?? new List<ProductPushDto>())
            {
                try
                {
                    var existing = (await _unitOfWork.Repository<Product>()
                        .FindAsync(p => p.SyncId == item.SyncId, ct)).FirstOrDefault();

                    // Without this, anyone who could sign in to the app could change sale prices.
                    if (existing != null && !await CanAsync(Mercurius.Common.ModuleRegistry.Pages.PRODUCTS_EDIT))
                    {
                        results.Add(new SyncPushResultItem { SyncId = item.SyncId, Error = "This account isn't allowed to edit products." });
                        continue;
                    }
                    if (existing == null && !await CanAsync(Mercurius.Common.ModuleRegistry.Pages.PRODUCTS_CREATE))
                    {
                        results.Add(new SyncPushResultItem { SyncId = item.SyncId, Error = "This account isn't allowed to add products." });
                        continue;
                    }

                    int? categoryId = null;
                    if (!string.IsNullOrWhiteSpace(item.CategoryName))
                    {
                        var category = (await _unitOfWork.Repository<ProductCategory>()
                            .FindAsync(c => c.Name == item.CategoryName, ct)).FirstOrDefault();
                        if (category == null)
                        {
                            category = new ProductCategory { Name = item.CategoryName };
                            await _unitOfWork.Repository<ProductCategory>().AddAsync(category, ct);
                            await _unitOfWork.SaveChangesAsync(ct);
                        }
                        categoryId = category.Id;
                    }

                    if (existing != null)
                    {
                        existing.ProductCode = item.ProductCode;
                        // null = "this app build doesn't know about barcodes" (keep whatever's set,
                        // e.g. from the web admin); "" = the user deliberately cleared it.
                        existing.Barcode = item.Barcode == null
                            ? existing.Barcode
                            : string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode.Trim();
                        existing.Name = item.Name;
                        existing.Description = item.Description ?? existing.Description;
                        existing.ProductCategoryId = categoryId ?? existing.ProductCategoryId;
                        existing.CurrentCostPrice = item.CurrentCostPrice;
                        existing.CurrentSalePrice = item.CurrentSalePrice;
                        // CurrentStock deliberately NOT taken from the device on update: a device's
                        // copy can be hours stale (offline, other terminals selling), and accepting it
                        // would silently undo every sale/adjustment since. Stock only moves through
                        // sales, adjustments, and shipments. (It IS accepted on create, below — a
                        // brand-new product's opening stock has nothing to conflict with.)
                        existing.LowStockCount = item.LowStockCount;
                        existing.MarkUpPercentage = item.MarkUpPercentage;
                        existing.IsActive = item.IsActive;
                        existing.UpdatedDate = now;
                        existing.UpdatedBy = CurrentUserId;

                        await _unitOfWork.Repository<Product>().UpdateAsync(existing, ct);
                        await _unitOfWork.SaveChangesAsync(ct);

                        results.Add(new SyncPushResultItem { SyncId = item.SyncId, Created = false, LastModifiedUtc = now });
                    }
                    else
                    {
                        var product = new Product
                        {
                            SyncId = item.SyncId,
                            ProductCode = item.ProductCode,
                            Barcode = string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode.Trim(),
                            Name = item.Name,
                            Description = item.Description ?? "",
                            ProductCategoryId = categoryId,
                            CurrentCostPrice = item.CurrentCostPrice,
                            CurrentSalePrice = item.CurrentSalePrice,
                            CurrentStock = item.CurrentStock,
                            LowStockCount = item.LowStockCount,
                            MarkUpPercentage = item.MarkUpPercentage,
                            IsActive = item.IsActive,
                            CreateDate = now,
                            CreatedBy = CurrentUserId
                        };

                        await _unitOfWork.Repository<Product>().AddAsync(product, ct);
                        await _unitOfWork.SaveChangesAsync(ct);

                        results.Add(new SyncPushResultItem { SyncId = item.SyncId, Created = true, LastModifiedUtc = now });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new SyncPushResultItem { SyncId = item.SyncId, Error = ex.Message });
                }
            }

            return Ok(results);
        }

        // ============================================
        // INVOICES (create-once — see class remarks)
        // ============================================

        [HttpGet("invoices/pull")]
        public IActionResult PullInvoices([FromQuery] DateTime? since, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var sinceValue = since ?? DateTime.MinValue;

            var invoices = _unitOfWork.Query<Invoice>()
                .Where(i => (i.UpdatedDate ?? i.CreatedDate) >= sinceValue)
                .OrderBy(i => i.UpdatedDate ?? i.CreatedDate)
                .Select(i => new InvoiceSyncDto
                {
                    SyncId = i.SyncId,
                    InvoiceDate = i.InvoiceDate,
                    InvoiceNumber = i.InvoiceNumber,
                    CustomerId = i.CustomerId,
                    LocationId = i.LocationId,
                    Notes = i.Notes,
                    PaidAmount = i.PaidAmount,
                    HasRefund = i.HasRefund,
                    LastModifiedUtc = i.UpdatedDate ?? i.CreatedDate,
                    Items = i.InvoiceItems.Select(ii => new InvoiceItemSyncDto
                    {
                        SyncId = ii.SyncId,
                        ProductSyncId = ii.Product.SyncId,
                        Quantity = ii.Quantity,
                        SalePrice = ii.SalePrice,
                        CostPrice = ii.CostPrice,
                        Remarks = ii.Remarks
                    }).ToList()
                })
                .ToList();

            return Ok(invoices);
        }

        // ============================================
        // RECEIPTS (every sale in the tenant, display-ready, for the mobile Receipts page)
        // ============================================
        //
        // Incremental and resumable. Pages are ordered by (last-modified, Id) and the cursor is
        // that pair — a plain "since timestamp" cursor either skips rows that share the boundary
        // timestamp (with >) or can loop forever on a page full of them (with >=). A refund or
        // any other change re-stamps the invoice (InvoiceChangeStampInterceptor), so it comes
        // round again and the device updates its copy.
        //
        // `fromDate` bounds how far back a device keeps history (it asks for its own retention
        // window, e.g. 30 days); it filters on the real-world InvoiceDate, not the cursor.
        private const int MaxReceiptPageSize = 200;

        [HttpGet("receipts/pull")]
        public async Task<IActionResult> PullReceipts(
            [FromQuery] string? cursor, [FromQuery] DateTime? fromDate, [FromQuery] int take = 100,
            CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, MaxReceiptPageSize);

            DateTime afterModified = DateTime.MinValue;
            int afterId = 0;
            if (!string.IsNullOrEmpty(cursor))
            {
                var parts = cursor.Split(':');
                if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !int.TryParse(parts[1], out afterId))
                {
                    return BadRequest(new { error = "Invalid cursor." });
                }
                afterModified = new DateTime(ticks, DateTimeKind.Utc);
            }

            var query = _unitOfWork.Query<Invoice>().AsNoTracking();
            if (fromDate.HasValue)
            {
                var from = fromDate.Value;
                query = query.Where(i => i.InvoiceDate >= from);
            }

            var page = await query
                .Where(i => (i.UpdatedDate ?? i.CreatedDate) > afterModified
                            || ((i.UpdatedDate ?? i.CreatedDate) == afterModified && i.Id > afterId))
                .OrderBy(i => i.UpdatedDate ?? i.CreatedDate).ThenBy(i => i.Id)
                .Take(take + 1) // one extra to know whether there's more
                .Select(i => new
                {
                    i.Id, i.SyncId, i.InvoiceNumber, i.InvoiceDate, i.LocationId, i.CreatedBy,
                    i.SourceChannel, i.SourceDeviceId, i.SourceDeviceName, i.Notes, i.PaymentMethod, i.PaidAmount,
                    i.HasRefund, i.StatusId,
                    Modified = i.UpdatedDate ?? i.CreatedDate,
                    Items = i.InvoiceItems.Select(ii => new
                    {
                        ii.Id, ii.SyncId, ProductSyncId = ii.Product.SyncId, ProductName = ii.Product.Name,
                        ii.Quantity, ii.SalePrice, ii.CustomTotalPrice
                    }).ToList()
                })
                .ToListAsync(ct);

            var hasMore = page.Count > take;
            if (hasMore) page.RemoveAt(page.Count - 1);

            // Names resolved in bulk after the page is fixed (no per-row lookups).
            var itemIds = page.SelectMany(p => p.Items.Select(i => i.Id)).ToList();
            var refundedByItem = itemIds.Count == 0
                ? new Dictionary<int, decimal>()
                : (await _unitOfWork.Query<InvoiceItemRefund>().AsNoTracking()
                    .Where(r => itemIds.Contains(r.InvoiceItemId))
                    .Select(r => new { r.InvoiceItemId, r.Quantity })
                    .ToListAsync(ct))
                  .GroupBy(r => r.InvoiceItemId)
                  .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

            var locationIds = page.Select(p => p.LocationId).Distinct().ToList();
            var locationNames = await _unitOfWork.Query<Location>().AsNoTracking()
                .Where(l => locationIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Name, ct);

            // Identity ids are Guid strings; CreatedBy is the same Guid.
            var cashierIds = page.Where(p => p.CreatedBy != Guid.Empty).Select(p => p.CreatedBy.ToString()).Distinct().ToList();
            var cashierNames = await _unitOfWork.Query<Mercurius.Repo.IdentityModel.MercuriusUser>().AsNoTracking()
                .Where(u => cashierIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync(ct);
            var cashierLookup = cashierNames.ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}".Trim()) ? u.Email : $"{u.FirstName} {u.LastName}".Trim());

            var result = new ReceiptPullPage { HasMore = hasMore };
            foreach (var inv in page)
            {
                var lines = inv.Items.Select(ii =>
                {
                    var lineTotal = ii.CustomTotalPrice ?? ii.Quantity * ii.SalePrice;
                    var refundedQty = refundedByItem.TryGetValue(ii.Id, out var q) ? q : 0m;
                    return new ReceiptLineSyncDto
                    {
                        SyncId = ii.SyncId,
                        ProductSyncId = ii.ProductSyncId,
                        ProductName = ii.ProductName ?? "",
                        Quantity = ii.Quantity,
                        SalePrice = ii.SalePrice,
                        LineTotal = lineTotal,
                        RefundedQuantity = refundedQty
                    };
                }).ToList();

                result.Items.Add(new ReceiptSyncDto
                {
                    SyncId = inv.SyncId,
                    InvoiceNumber = inv.InvoiceNumber ?? "",
                    InvoiceDate = DateTime.SpecifyKind(inv.InvoiceDate, DateTimeKind.Utc),
                    LocationId = inv.LocationId,
                    LocationName = locationNames.TryGetValue(inv.LocationId, out var ln) ? ln : null,
                    CashierName = inv.CreatedBy != Guid.Empty && cashierLookup.TryGetValue(inv.CreatedBy.ToString(), out var cn) ? cn : null,
                    SourceChannel = inv.SourceChannel,
                    SourceDeviceId = inv.SourceDeviceId,
                    SourceDeviceName = inv.SourceDeviceName,
                    Notes = inv.Notes,
                    PaymentMethod = Mercurius.Services.PaymentMethods.Normalize(inv.PaymentMethod, inv.Notes),
                    PaidAmount = inv.PaidAmount,
                    GrandTotal = lines.Sum(l => l.LineTotal),
                    RefundedAmount = lines.Sum(l => l.Quantity == 0 ? 0 : l.LineTotal / l.Quantity * l.RefundedQuantity),
                    HasRefund = inv.HasRefund,
                    Status = ((StatusCollection.InvoiceStatus)inv.StatusId).ToString(),
                    LastModifiedUtc = DateTime.SpecifyKind(inv.Modified, DateTimeKind.Utc),
                    Items = lines
                });
            }

            var last = page.LastOrDefault();
            result.NextCursor = last != null
                ? $"{DateTime.SpecifyKind(last.Modified, DateTimeKind.Utc).Ticks}:{last.Id}"
                : cursor; // nothing new — the caller keeps its existing position
            return Ok(result);
        }

        [HttpPost("invoices/push")]
        public async Task<IActionResult> PushInvoices([FromBody] List<InvoicePushDto> items, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var results = new List<SyncPushResultItem>();
            var deviceId = HeaderOrNull(Mercurius.Services.Auditing.HttpAuditContextProvider.DeviceIdHeader);
            var deviceName = HeaderOrNull(Mercurius.Services.Auditing.HttpAuditContextProvider.DeviceNameHeader);
            var canSell = await CanAsync(Mercurius.Common.ModuleRegistry.Pages.NEWSALE_CREATE);

            foreach (var item in items ?? new List<InvoicePushDto>())
            {
                // Per invoice, not per batch: a device that was offline pushes many sales at once,
                // and one shared timestamp gave them all the SAME InvoiceNumber — violating its
                // unique index and failing every sale after the first, on every retry, forever.
                var now = DateTime.UtcNow;
                var transactionStarted = false;
                try
                {
                    var existing = (await _unitOfWork.Repository<Invoice>()
                        .FindAsync(i => i.SyncId == item.SyncId, ct)).FirstOrDefault();

                    if (existing != null)
                    {
                        // Already synced — invoices are create-once, so this push is a no-op.
                        results.Add(new SyncPushResultItem { SyncId = item.SyncId, Created = false, LastModifiedUtc = existing.UpdatedDate ?? existing.CreatedDate });
                        continue;
                    }

                    if (!canSell)
                    {
                        results.Add(new SyncPushResultItem { SyncId = item.SyncId, Error = "This account isn't allowed to make sales." });
                        continue;
                    }
                    if (item.Items.Count == 0 || item.Items.Any(i => i.Quantity <= 0))
                    {
                        // A zero or negative quantity would put stock back on the shelf.
                        results.Add(new SyncPushResultItem { SyncId = item.SyncId, Error = "Every item in a sale needs a quantity above zero." });
                        continue;
                    }

                    // Mercurius.Mobile's Settings page lets the device itself be assigned to a
                    // clinic (see GetLocations above) — a shared POS terminal's branch doesn't
                    // depend on who's signed in on it, unlike the web app's per-user
                    // UserCurrentLocation. Trust that device-sent value once it's a real Location
                    // within this tenant (Query<Location>() is already tenant-filtered, so a
                    // cross-tenant id simply won't be found here). Devices that haven't been
                    // assigned a clinic yet still send 0 — fall back to the old per-user
                    // resolution for those, same as SalesController.NewSale, rather than leaving
                    // the invoice's LocationId at 0 (which dashboard widgets that scope by branch
                    // would silently exclude).
                    var deviceLocationExists = item.LocationId > 0 &&
                        await _unitOfWork.Query<Location>().AnyAsync(l => l.Id == item.LocationId, ct);
                    var resolvedLocationId = deviceLocationExists
                        ? item.LocationId
                        : await Mercurius.Services.CurrentLocation.GetIdAsync(_unitOfWork, User);

                    var cashierId = await ResolveCashierAsync(item.CashierUserId, ct);

                    // All-or-nothing per sale. Previously the invoice header was committed before its
                    // lines; if a line then failed, the device retried, the server saw the SyncId
                    // already existed ("create-once, no-op"), and the lines were lost for good.
                    await _unitOfWork.BeginTransactionAsync(ct);
                    transactionStarted = true;

                    var invoice = new Invoice
                    {
                        SyncId = item.SyncId,
                        InvoiceDate = item.InvoiceDate,
                        InvoiceNumber = Mercurius.Services.SaleRecorder.NewInvoiceNumber(now, item.SyncId),
                        CustomerId = item.CustomerId,
                        LocationId = resolvedLocationId,
                        StatusId = (int)StatusCollection.InvoiceStatus.Completed,
                        Notes = item.Notes,
                        // Older app builds only say how it was paid in the notes.
                        PaymentMethod = Mercurius.Services.PaymentMethods.Normalize(item.PaymentMethod, item.Notes),
                        PaidAmount = item.PaidAmount,
                        CreatedDate = now,
                        CreatedBy = cashierId,
                        SourceChannel = "MobileApp",
                        SourceDeviceId = deviceId,
                        SourceDeviceName = deviceName
                    };
                    await _unitOfWork.Repository<Invoice>().AddAsync(invoice, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    // Same inventory bookkeeping as a web sale (SaleRecorder): the price the device
                    // charged is kept (it's what the customer paid, possibly offline hours ago), but
                    // which batch it's drawn from, its cost, and the stock ledger are decided here.
                    var soldBy = new Mercurius.Services.SaleRecorder.SoldBy(CurrentUserId, User.Identity?.Name ?? "Unknown", "mobile");
                    var productsSold = new HashSet<int>();
                    foreach (var lineItem in item.Items)
                    {
                        var product = (await _unitOfWork.Repository<Product>()
                            .FindAsync(p => p.SyncId == lineItem.ProductSyncId, ct)).FirstOrDefault();
                        if (product == null)
                        {
                            // Product hasn't reached this server yet (e.g. created offline on the
                            // device). Fail the whole sale so the device retries it after its
                            // product push — silently dropping the line (the old behavior) lost it
                            // permanently, since a retry of an existing invoice is a no-op.
                            throw new InvalidOperationException(
                                $"Product {lineItem.ProductSyncId} is not on the server yet; the sale will be retried after products sync.");
                        }

                        var quote = await _saleRecorder.QuoteAsync(_unitOfWork, product, lineItem.Quantity, ct);
                        await _saleRecorder.RecordLineAsync(_unitOfWork, invoice, product, lineItem.Quantity, quote, lineItem.SalePrice,
                            soldBy, lineItem.SyncId, lineItem.Remarks, ct);
                        productsSold.Add(product.Id);
                    }
                    await _unitOfWork.SaveChangesAsync(ct);
                    await _saleRecorder.RefreshPricesAsync(_unitOfWork, productsSold, ct);

                    await _unitOfWork.CommitTransactionAsync(ct);
                    transactionStarted = false;

                    results.Add(new SyncPushResultItem { SyncId = item.SyncId, Created = true, LastModifiedUtc = now });
                }
                catch (Exception ex)
                {
                    if (transactionStarted)
                    {
                        // Also clears the change tracker (see EfUnitOfWork.RollbackTransactionAsync),
                        // so this sale's half-built rows aren't re-attempted by the next sale's save.
                        try { await _unitOfWork.RollbackTransactionAsync(CancellationToken.None); } catch { /* best-effort */ }
                    }
                    results.Add(new SyncPushResultItem { SyncId = item.SyncId, Error = ex.Message });
                }
            }

            return Ok(results);
        }

        // The device-reported cashier is only accepted if it's a real user in the SAME tenant as the
        // pushing user (users aren't tenant-filtered, so this is checked explicitly) — a device
        // can't attribute a sale to someone in another pharmacy. Anything else: the pusher.
        private async Task<Guid> ResolveCashierAsync(string? deviceCashierUserId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(deviceCashierUserId)
                || !Guid.TryParse(deviceCashierUserId, out var claimed)
                || claimed == CurrentUserId)
            {
                return CurrentUserId;
            }

            var tenantClaim = User.FindFirstValue(MercuriusClaimTypes.TenantId);
            if (!int.TryParse(tenantClaim, out var tenantId)) return CurrentUserId;

            var claimedIdString = claimed.ToString();
            var sameTenant = await _unitOfWork.Query<Mercurius.Repo.IdentityModel.MercuriusUser>()
                .AnyAsync(u => u.Id == claimedIdString && u.TenantId == tenantId, ct);
            return sameTenant ? claimed : CurrentUserId;
        }

        private string? HeaderOrNull(string name)
        {
            var value = Request.Headers[name].ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
