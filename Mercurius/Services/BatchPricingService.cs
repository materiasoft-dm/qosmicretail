using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Services
{
    /// <summary>
    /// Implements FIFO batch pricing for batch-tracked products (see MedicineBatch). A new
    /// shipment at a different price is just a new batch queued behind older stock — the price
    /// charged only rolls over once older batches are fully depleted.
    ///
    /// By design (confirmed with the product owner), a sale is never split across two batches'
    /// price/stock ledgers: if the oldest batch alone can't cover the requested quantity, the
    /// whole line is priced and drawn from the next batch that can, rather than partially
    /// consuming both. This keeps checkout simple at the cost of occasionally under-counting a
    /// few leftover units on an old batch when quantities don't line up exactly.
    /// </summary>
    public class BatchPricingService
    {
        public async Task<MedicineBatch?> FindFulfillingBatchAsync(
            IUnitOfWork unitOfWork, int productId, decimal quantity, CancellationToken ct = default)
        {
            var batches = (await unitOfWork.Repository<MedicineBatch>()
                .FindAsync(b => b.ProductId == productId && b.IsActive && b.RemainingQuantity > 0, ct))
                .OrderBy(b => b.ReceivedDate)
                .ToList();

            if (batches.Count == 0) return null;

            // The oldest batch that alone has enough to cover the whole line; if none does
            // (every remaining batch is smaller than the requested quantity), fall back to the
            // newest one rather than blocking the sale.
            return batches.FirstOrDefault(b => b.RemainingQuantity >= quantity) ?? batches.Last();
        }

        /// <summary>
        /// Re-syncs Product.CurrentSalePrice/CurrentCostPrice to whichever batch is now the
        /// oldest with stock remaining, so every screen that reads those flat fields (product
        /// lists, DataTables, the mobile sync API) keeps working without needing to know batches
        /// exist at all. No-ops if the product has no active batch with stock left — the last
        /// known price is left in place until it's restocked.
        /// </summary>
        public async Task RefreshActivePriceAsync(IUnitOfWork unitOfWork, int productId, CancellationToken ct = default)
        {
            var activeBatch = (await unitOfWork.Repository<MedicineBatch>()
                .FindAsync(b => b.ProductId == productId && b.IsActive && b.RemainingQuantity > 0, ct))
                .OrderBy(b => b.ReceivedDate)
                .FirstOrDefault();

            if (activeBatch == null) return;

            var product = await unitOfWork.Repository<Product>().GetByIdAsync(productId, ct);
            if (product == null) return;

            product.CurrentSalePrice = activeBatch.UnitSalePrice;
            product.CurrentCostPrice = activeBatch.UnitCost;
            await unitOfWork.Repository<Product>().UpdateAsync(product, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
