using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Services
{
    /// <summary>
    /// Applies an Adjustment's quantity to its product's stock, in the direction its reason's
    /// IsInbound says. Manual adjustments (AdjustmentsApiController) and refund restocks
    /// (InvoicesApiController.CreateRefund) both go through here, so they move stock the same way.
    /// </summary>
    public class AdjustmentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdjustmentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Adds or subtracts <paramref name="adjustment"/>.Quantity from its product's
        /// CurrentStock, based on whether its Reason is inbound. Assumes the Adjustment row
        /// itself has already been saved — this only updates the Product.
        /// </summary>
        public async Task ApplyAsync(Adjustment adjustment, CancellationToken ct = default)
        {
            var reason = await _unitOfWork.Repository<AdjustmentReason>().GetByIdAsync(adjustment.ReasonId, ct);
            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(adjustment.ProductId, ct);
            if (reason == null || product == null) return;

            product.CurrentStock += reason.IsInbound ? adjustment.Quantity : -adjustment.Quantity;
            await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}
