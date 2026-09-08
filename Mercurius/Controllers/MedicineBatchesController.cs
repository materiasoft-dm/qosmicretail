using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Authorize]
    public class MedicineBatchesController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.BatchPricingService _batchPricingService;

        public MedicineBatchesController(
            IHttpContextAccessor httpContextAccessor,
            IUnitOfWork unitOfWork,
            Mercurius.Services.BatchPricingService batchPricingService)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _batchPricingService = batchPricingService;
        }

        /// <summary>List batches for a specific product.</summary>
        public async Task<IActionResult> Index(int productId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(productId, ct);
            if (product == null) return NotFound();

            var batches = await _unitOfWork.Repository<MedicineBatch>()
                .FindAsync(b => b.ProductId == productId, ct);

            ViewBag.Product = product;
            return View(batches.OrderBy(b => b.ExpiryDate));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MedicineBatch batch, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                batch.ReceivedDate = DateTime.UtcNow;
                batch.RemainingQuantity = batch.InitialQuantity;
                batch.IsActive = true;

                await _unitOfWork.Repository<MedicineBatch>().AddAsync(batch, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // Update product stock
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(batch.ProductId, ct);
                if (product != null)
                {
                    product.CurrentStock += batch.InitialQuantity;
                    await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                }

                // Only actually changes the product's displayed price if this new batch turns
                // out to be the oldest one with stock (e.g. it's the first batch ever, or every
                // prior batch is already depleted) — otherwise the existing older batch's price
                // still wins, which is the whole point of FIFO batch pricing.
                await _batchPricingService.RefreshActivePriceAsync(_unitOfWork, batch.ProductId, ct);

                return RedirectToAction(nameof(Index), new { productId = batch.ProductId });
            }
            return RedirectToAction(nameof(Index), new { productId = batch.ProductId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var batch = await _unitOfWork.Repository<MedicineBatch>().GetByIdAsync(id, ct);
            if (batch != null)
            {
                batch.IsActive = false;
                await _unitOfWork.Repository<MedicineBatch>().UpdateAsync(batch, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index), new { productId = batch.ProductId });
            }
            return RedirectToAction("Index", "Products");
        }
    }
}
