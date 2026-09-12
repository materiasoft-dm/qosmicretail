using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.MedicineBatches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // JSON equivalent of MedicineBatchesController — Create bumps Product.CurrentStock by the
    // new batch's InitialQuantity and refreshes the product's active FIFO price, exactly as the
    // MVC action does; Delete is a soft-delete that deliberately does NOT reverse the stock bump
    // (matching the MVC controller's existing, documented behavior).
    [ApiController]
    [Route("api/medicine-batches")]
    [Authorize]
    public class MedicineBatchesApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.BatchPricingService _batchPricingService;

        public MedicineBatchesApiController(IUnitOfWork unitOfWork, Mercurius.Services.BatchPricingService batchPricingService)
        {
            _unitOfWork = unitOfWork;
            _batchPricingService = batchPricingService;
        }

        [HttpGet]
        public async Task<ActionResult<MedicineBatchListResultDto>> Get(int productId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(productId, ct);
            if (product == null) return NotFound();

            var items = _unitOfWork.Query<MedicineBatch>().Where(b => b.ProductId == productId)
                .OrderBy(b => b.ExpiryDate)
                .Select(b => new MedicineBatchDto
                {
                    Id = b.Id,
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    ReceivedDate = b.ReceivedDate,
                    UnitCost = b.UnitCost,
                    UnitSalePrice = b.UnitSalePrice,
                    InitialQuantity = b.InitialQuantity,
                    RemainingQuantity = b.RemainingQuantity
                }).ToList();

            return Ok(new MedicineBatchListResultDto { ProductName = product.Name ?? string.Empty, Items = items });
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateMedicineBatchRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.BatchNumber) || request.InitialQuantity <= 0)
            {
                return BadRequest(new { error = "Batch number and a positive initial quantity are required." });
            }

            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(request.ProductId, ct);
            if (product == null) return BadRequest(new { error = "Product not found." });

            var batch = new MedicineBatch
            {
                ProductId = request.ProductId,
                BatchNumber = request.BatchNumber,
                ExpiryDate = request.ExpiryDate,
                UnitCost = request.UnitCost,
                UnitSalePrice = request.UnitSalePrice,
                InitialQuantity = request.InitialQuantity,
                ReceivedDate = DateTime.UtcNow,
                RemainingQuantity = request.InitialQuantity,
                IsActive = true
            };
            await _unitOfWork.Repository<MedicineBatch>().AddAsync(batch, ct);

            product.CurrentStock += request.InitialQuantity;
            await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _batchPricingService.RefreshActivePriceAsync(_unitOfWork, batch.ProductId, ct);

            return Ok(new { id = batch.Id });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            var batch = await _unitOfWork.Repository<MedicineBatch>().GetByIdAsync(id, ct);
            if (batch == null) return NotFound();
            batch.IsActive = false;
            await _unitOfWork.Repository<MedicineBatch>().UpdateAsync(batch, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok();
        }
    }
}
