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
        private readonly Mercurius.Services.BatchPricingService _batchPricingService;

        public SyncController(IUnitOfWork unitOfWork, Mercurius.Services.BatchPricingService batchPricingService)
        {
            _unitOfWork = unitOfWork;
            _batchPricingService = batchPricingService;
        }

        private Guid CurrentUserId =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;

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
                        existing.Name = item.Name;
                        existing.Description = item.Description ?? existing.Description;
                        existing.ProductCategoryId = categoryId ?? existing.ProductCategoryId;
                        existing.CurrentCostPrice = item.CurrentCostPrice;
                        existing.CurrentSalePrice = item.CurrentSalePrice;
                        existing.CurrentStock = item.CurrentStock;
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

        [HttpPost("invoices/push")]
        public async Task<IActionResult> PushInvoices([FromBody] List<InvoicePushDto> items, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var results = new List<SyncPushResultItem>();
            var now = DateTime.UtcNow;

            foreach (var item in items ?? new List<InvoicePushDto>())
            {
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

                    var invoice = new Invoice
                    {
                        SyncId = item.SyncId,
                        InvoiceDate = item.InvoiceDate,
                        InvoiceNumber = $"INV-{now:yyyyMMddHHmmssfff}",
                        CustomerId = item.CustomerId,
                        LocationId = item.LocationId,
                        StatusId = (int)StatusCollection.InvoiceStatus.Draft,
                        Notes = item.Notes,
                        PaidAmount = item.PaidAmount,
                        CreatedDate = now,
                        CreatedBy = CurrentUserId
                    };
                    await _unitOfWork.Repository<Invoice>().AddAsync(invoice, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    // Mirrors SalesController.NewSale's inventory bookkeeping: the price the
                    // device actually charged is trusted as-is (it's what the customer agreed to
                    // pay), but which batch it's drawn from — and the resulting stock/COGS
                    // ledger — is decided here, server-side, same as any web-created sale. Without
                    // this, a synced mobile sale would record revenue without ever touching
                    // inventory, silently drifting Product.CurrentStock out of sync with reality.
                    var productsWithBatchActivity = new HashSet<int>();
                    foreach (var lineItem in item.Items)
                    {
                        var product = (await _unitOfWork.Repository<Product>()
                            .FindAsync(p => p.SyncId == lineItem.ProductSyncId, ct)).FirstOrDefault();
                        if (product == null)
                        {
                            // Product hasn't synced to this server yet — skip the line rather than
                            // fail the whole invoice; the client should retry once products sync.
                            continue;
                        }

                        var batch = await _batchPricingService.FindFulfillingBatchAsync(_unitOfWork, product.Id, lineItem.Quantity, ct);

                        var invoiceItem = new InvoiceItem
                        {
                            SyncId = lineItem.SyncId,
                            InvoiceId = invoice.Id,
                            ProductId = product.Id,
                            Quantity = lineItem.Quantity,
                            SalePrice = lineItem.SalePrice,
                            CostPrice = lineItem.CostPrice,
                            MedicineBatchId = batch?.Id,
                            Remarks = lineItem.Remarks ?? "",
                            StatusId = (int)StatusCollection.InvoiceStatus.Draft
                        };
                        await _unitOfWork.Repository<InvoiceItem>().AddAsync(invoiceItem, ct);

                        if (batch != null)
                        {
                            batch.RemainingQuantity -= lineItem.Quantity;
                            await _unitOfWork.Repository<MedicineBatch>().UpdateAsync(batch, ct);
                        }
                        product.CurrentStock -= lineItem.Quantity;
                        await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
                        productsWithBatchActivity.Add(product.Id);

                        if (product.CurrentStock <= 0)
                        {
                            var auditLog = new ZeroStockSaleAuditLog
                            {
                                ProductId = product.Id,
                                ProductName = product.Name,
                                ProductCode = product.ProductCode,
                                QuantitySold = lineItem.Quantity,
                                StockAtTimeOfSale = product.CurrentStock,
                                InvoiceNumber = invoice.InvoiceNumber,
                                InvoiceId = invoice.Id,
                                SoldByUserId = CurrentUserId,
                                SoldByUserName = User.Identity?.Name ?? "Unknown",
                                SaleDate = now,
                                Notes = $"Zero-stock mobile sale: inventory was {product.CurrentStock} before this sale of {lineItem.Quantity} units"
                            };
                            await _unitOfWork.Repository<ZeroStockSaleAuditLog>().AddAsync(auditLog, ct);
                        }
                    }
                    await _unitOfWork.SaveChangesAsync(ct);

                    foreach (var productId in productsWithBatchActivity)
                    {
                        await _batchPricingService.RefreshActivePriceAsync(_unitOfWork, productId, ct);
                    }

                    results.Add(new SyncPushResultItem { SyncId = item.SyncId, Created = true, LastModifiedUtc = now });
                }
                catch (Exception ex)
                {
                    results.Add(new SyncPushResultItem { SyncId = item.SyncId, Error = ex.Message });
                }
            }

            return Ok(results);
        }
    }
}
