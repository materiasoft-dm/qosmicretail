using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Services
{
    /// <summary>
    /// The inventory side of recording a sale, shared by web checkout (SalesApiController) and
    /// mobile sync (SyncController.PushInvoices) so the two tills can't drift apart again: which
    /// batch a line is priced and drawn from, the line's cost, the stock and batch deductions,
    /// and the zero-stock audit log. What the customer is charged is the caller's decision
    /// (see SalesApiController and PushInvoices for when the till's own price is kept).
    /// </summary>
    public class SaleRecorder
    {
        private readonly BatchPricingService _batchPricing;

        public SaleRecorder(BatchPricingService batchPricing)
        {
            _batchPricing = batchPricing;
        }

        /// <summary>What the server would charge for a line right now, and which batch it comes from.</summary>
        public sealed record LineQuote(MedicineBatch? Batch, decimal SalePrice, decimal CostPrice);

        public async Task<LineQuote> QuoteAsync(IUnitOfWork unitOfWork, Product product, decimal quantity, CancellationToken ct = default)
        {
            var batch = await _batchPricing.FindFulfillingBatchAsync(unitOfWork, product.Id, quantity, ct);
            return new LineQuote(
                batch,
                batch?.UnitSalePrice ?? product.CurrentSalePrice ?? 0,
                batch?.UnitCost ?? product.CurrentCostPrice ?? 0);
        }

        /// <summary>
        /// Adds the invoice line and takes its quantity out of stock. <paramref name="salePrice"/>
        /// is what the customer was charged; the cost always comes from the quote, since it's the
        /// server's own bookkeeping, not something a till observed.
        /// </summary>
        public async Task<InvoiceItem> RecordLineAsync(
            IUnitOfWork unitOfWork, Invoice invoice, Product product, decimal quantity, LineQuote quote, decimal salePrice,
            SoldBy soldBy, Guid? lineSyncId = null, string? remarks = null, CancellationToken ct = default)
        {
            var item = new InvoiceItem
            {
                InvoiceId = invoice.Id,
                ProductId = product.Id,
                Quantity = quantity,
                SalePrice = salePrice,
                CostPrice = quote.CostPrice,
                MedicineBatchId = quote.Batch?.Id,
                Remarks = remarks ?? "",
                StatusId = (int)StatusCollection.InvoiceStatus.Completed
            };
            if (lineSyncId is Guid syncId && syncId != Guid.Empty) item.SyncId = syncId;
            await unitOfWork.Repository<InvoiceItem>().AddAsync(item, ct);

            if (quote.Batch != null)
            {
                await _batchPricing.DeductAsync(unitOfWork, quote.Batch, quantity, ct);
            }

            var stockBefore = product.CurrentStock;
            product.CurrentStock -= quantity;
            await unitOfWork.Repository<Product>().UpdateAsync(product, ct);

            if (product.CurrentStock <= 0)
            {
                await unitOfWork.Repository<ZeroStockSaleAuditLog>().AddAsync(new ZeroStockSaleAuditLog
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductCode = product.ProductCode,
                    QuantitySold = quantity,
                    StockAtTimeOfSale = stockBefore,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceId = invoice.Id,
                    SoldByUserId = soldBy.UserId,
                    SoldByUserName = soldBy.UserName,
                    SaleDate = DateTime.UtcNow,
                    Notes = $"Zero-stock {soldBy.Channel} sale: inventory was {stockBefore:0.##} before this sale of {quantity:0.##} units"
                }, ct);
            }

            return item;
        }

        /// <summary>Moves each product's displayed price to its oldest batch that still has stock.</summary>
        public async Task RefreshPricesAsync(IUnitOfWork unitOfWork, IEnumerable<int> productIds, CancellationToken ct = default)
        {
            foreach (var productId in productIds.Distinct())
            {
                await _batchPricing.RefreshActivePriceAsync(unitOfWork, productId, ct);
            }
        }

        /// <summary>
        /// INV-&lt;utc ms&gt;-XXXX. The SyncId suffix keeps two sales made in the same millisecond
        /// (two tills, or a burst of offline sales replayed at once) from colliding on the
        /// unique invoice number.
        /// </summary>
        public static string NewInvoiceNumber(DateTime utcNow, Guid syncId) =>
            $"INV-{utcNow:yyyyMMddHHmmssfff}-{syncId.ToString("N").Substring(0, 4).ToUpperInvariant()}";

        public sealed record SoldBy(Guid UserId, string UserName, string Channel);
    }

    /// <summary>How a sale was paid, stored in Invoice.PaymentMethod.</summary>
    public static class PaymentMethods
    {
        public const string Cash = "Cash";
        public const string Card = "Card";

        /// <summary>
        /// The method a till sent, or for older tills that only wrote "Paid by card"/"Paid by cash"
        /// into the notes, the method those notes name. Anything unrecognised is treated as cash.
        /// </summary>
        public static string Normalize(string? method, string? notes = null)
        {
            if (string.Equals(method?.Trim(), Card, StringComparison.OrdinalIgnoreCase)) return Card;
            if (string.Equals(method?.Trim(), Cash, StringComparison.OrdinalIgnoreCase)) return Cash;
            return notes != null && notes.Contains("Paid by card", StringComparison.OrdinalIgnoreCase) ? Card : Cash;
        }
    }
}
