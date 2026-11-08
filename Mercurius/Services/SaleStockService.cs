using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Services
{
    /// <summary>
    /// The stock side of selling one line: picks the FIFO batch it's drawn from, takes the quantity
    /// off that batch and the product, and logs a ZeroStockSaleAuditLog row when the product hits
    /// zero or below (selling is never blocked on stock). Web checkout (SalesApiController) and
    /// wholesale orders (WholesaleApiController) both go through here, so they move inventory the
    /// same way; SyncController.PushInvoices still has its own copy of these rules.
    ///
    /// Runs inside the caller's transaction and doesn't save. Once committed, call
    /// BatchPricingService.RefreshActivePriceAsync for each product touched.
    /// </summary>
    public class SaleStockService
    {
        private readonly BatchPricingService _batchPricingService;

        public SaleStockService(BatchPricingService batchPricingService)
        {
            _batchPricingService = batchPricingService;
        }

        /// <summary>
        /// Deducts <paramref name="quantity"/> of <paramref name="product"/> for a line on
        /// <paramref name="invoice"/> (already saved, so it has an Id). Returns the batch the line
        /// was drawn from — the caller prices the line from it (its cost, and for retail its sale
        /// price) — or null if the product isn't batch-tracked.
        /// </summary>
        public async Task<MedicineBatch?> DeductAsync(IUnitOfWork unitOfWork, Product product, decimal quantity,
            Invoice invoice, ClaimsPrincipal user, CancellationToken ct = default)
        {
            var batch = await _batchPricingService.FindFulfillingBatchAsync(unitOfWork, product.Id, quantity, ct);
            if (batch != null)
            {
                batch.RemainingQuantity -= quantity;
                await unitOfWork.Repository<MedicineBatch>().UpdateAsync(batch, ct);
            }

            product.CurrentStock -= quantity;
            await unitOfWork.Repository<Product>().UpdateAsync(product, ct);

            if (product.CurrentStock <= 0)
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                await unitOfWork.Repository<ZeroStockSaleAuditLog>().AddAsync(new ZeroStockSaleAuditLog
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductCode = product.ProductCode,
                    QuantitySold = quantity,
                    StockAtTimeOfSale = product.CurrentStock,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceId = invoice.Id,
                    SoldByUserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
                    SoldByUserName = user.Identity?.Name ?? "Unknown",
                    SaleDate = DateTime.UtcNow,
                    Notes = $"Zero-stock sale: inventory was {product.CurrentStock} before this sale of {quantity} units"
                }, ct);
            }

            return batch;
        }
    }
}
