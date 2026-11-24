using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Services
{
    /// <summary>
    /// Applies an Adjustment's quantity to its product's stock, in the direction its reason's
    /// IsInbound says. Manual adjustments (AdjustmentsApiController), refund restocks
    /// (InvoicesApiController.CreateRefund) and stock counts from a CSV import all go through here,
    /// so they move stock the same way.
    ///
    /// A write-off (outbound) also comes out of the product's batches, oldest first, exactly as a
    /// sale would: expired or damaged units are gone from the shelf, so they must stop being offered
    /// to FIFO pricing too, or the batches add up to more than the stock and the price charged
    /// stays on a batch that no longer exists. Inbound adjustments leave the batches alone — a refund
    /// restock puts units back on their own batch itself, and a plain "found 5 more" has no batch.
    /// </summary>
    public class AdjustmentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly BatchPricingService _batchPricing;

        public AdjustmentService(IUnitOfWork unitOfWork, BatchPricingService batchPricing)
        {
            _unitOfWork = unitOfWork;
            _batchPricing = batchPricing;
        }

        /// <summary>
        /// Adds or subtracts <paramref name="adjustment"/>.Quantity from its product's
        /// CurrentStock, based on whether its Reason is inbound. Assumes the Adjustment row
        /// itself has already been saved — this only updates the Product (and its batches).
        /// </summary>
        public async Task ApplyAsync(Adjustment adjustment, CancellationToken ct = default)
        {
            var reason = await _unitOfWork.Repository<AdjustmentReason>().GetByIdAsync(adjustment.ReasonId, ct);
            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(adjustment.ProductId, ct);
            if (reason == null || product == null) return;

            var drewFromBatches = false;
            if (!reason.IsInbound)
            {
                var batch = await _batchPricing.FindFulfillingBatchAsync(_unitOfWork, product.Id, adjustment.Quantity, ct);
                if (batch != null)
                {
                    await _batchPricing.DeductAsync(_unitOfWork, batch, adjustment.Quantity, ct);
                    drewFromBatches = true;
                }
            }

            product.CurrentStock += reason.IsInbound ? adjustment.Quantity : -adjustment.Quantity;
            await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // The displayed price follows whichever batch is now the oldest with stock.
            if (drewFromBatches) await _batchPricing.RefreshActivePriceAsync(_unitOfWork, product.Id, ct);
        }
    }
}
