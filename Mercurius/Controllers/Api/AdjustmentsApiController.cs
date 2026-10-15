using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Adjustments;
using Mercurius.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // Stock adjustments. The stock change itself (AdjustmentService.ApplyAsync) happens here on the
    // server; the page only ever sees the resulting list, never re-implements the stock math.
    [ApiController]
    [Route("api/adjustments")]
    [Authorize]
    public class AdjustmentsApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.AdjustmentService _adjustmentService;

        public AdjustmentsApiController(IUnitOfWork unitOfWork, Mercurius.Services.AdjustmentService adjustmentService)
        {
            _unitOfWork = unitOfWork;
            _adjustmentService = adjustmentService;
        }

        // Search matches the note, product name or product code. sortBy: date (default) | quantity.
        [HttpGet]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADJUSTMENTS_INDEX)]
        public ActionResult<ListResultDto<AdjustmentListItemDto>> Get(string? search = null, int page = 1, int pageSize = 25,
            string? sortBy = null, bool desc = true, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200;

            var query = _unitOfWork.Query<Adjustment>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                var productIds = _unitOfWork.Query<Product>()
                    .Where(p => (p.Name != null && p.Name.ToLower().Contains(s)) || (p.ProductCode != null && p.ProductCode.ToLower().Contains(s)))
                    .Select(p => p.Id);
                query = query.Where(a => (a.Note != null && a.Note.ToLower().Contains(s)) || productIds.Contains(a.ProductId));
            }

            var total = query.Count();
            // SQLite can't ORDER BY a decimal column, so quantity sorts as a double.
            IOrderedQueryable<Adjustment> ordered = (sortBy ?? "date").ToLowerInvariant() switch
            {
                "quantity" => desc ? query.OrderByDescending(a => (double)a.Quantity) : query.OrderBy(a => (double)a.Quantity),
                _ => desc ? query.OrderByDescending(a => a.AdjustmentDate) : query.OrderBy(a => a.AdjustmentDate)
            };
            var pageItems = ordered.ThenByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var reasonIds = pageItems.Select(a => a.ReasonId).Distinct().ToList();
            var reasonLookup = _unitOfWork.Query<AdjustmentReason>().Where(r => reasonIds.Contains(r.Id)).ToDictionary(r => r.Id);

            var pageProductIds = pageItems.Select(a => a.ProductId).Distinct().ToList();
            var productLookup = _unitOfWork.Query<Product>().Where(p => pageProductIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.ProductCode }).ToDictionary(p => p.Id);

            var refundIds = pageItems.Where(a => a.InvoiceItemRefundId.HasValue).Select(a => a.InvoiceItemRefundId!.Value).Distinct().ToList();
            var invoiceIdLookup = refundIds.Count == 0
                ? new Dictionary<int, int>()
                : _unitOfWork.Query<InvoiceItemRefund>().Where(r => refundIds.Contains(r.Id)).ToDictionary(r => r.Id, r => r.InvoiceId);

            var items = pageItems.Select(a =>
            {
                reasonLookup.TryGetValue(a.ReasonId, out var reason);
                productLookup.TryGetValue(a.ProductId, out var product);
                return new AdjustmentListItemDto
                {
                    Id = a.Id,
                    Date = a.AdjustmentDate,
                    ProductName = product?.Name ?? "(deleted product)",
                    ProductCode = product?.ProductCode ?? string.Empty,
                    ReasonName = reason?.Name ?? string.Empty,
                    IsInbound = reason?.IsInbound ?? false,
                    Quantity = a.Quantity,
                    Note = string.IsNullOrWhiteSpace(a.Note) ? null : a.Note,
                    InvoiceId = a.InvoiceItemRefundId.HasValue && invoiceIdLookup.TryGetValue(a.InvoiceItemRefundId.Value, out var invId) ? invId : null
                };
            }).ToList();

            return Ok(new ListResultDto<AdjustmentListItemDto> { Items = items, Total = total });
        }

        [HttpGet("reasons")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADJUSTMENTS_CREATE)]
        public ActionResult<List<AdjustmentReasonOptionDto>> GetReasons()
        {
            var items = _unitOfWork.Query<AdjustmentReason>().Where(r => r.IsActive).OrderBy(r => r.Name)
                .Select(r => new AdjustmentReasonOptionDto { Id = r.Id, Name = r.Name ?? string.Empty, IsInbound = r.IsInbound }).ToList();
            return Ok(items);
        }

        [HttpGet("products")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADJUSTMENTS_CREATE)]
        public ActionResult<List<ProductOptionDto>> GetProducts(string? search = null)
        {
            var query = _unitOfWork.Query<Product>().Where(p => p.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(p => (p.Name != null && p.Name.ToLower().Contains(s)) || (p.ProductCode != null && p.ProductCode.ToLower().Contains(s)));
            }
            var items = query.OrderBy(p => p.Name).Take(50)
                .Select(p => new ProductOptionDto { Id = p.Id, Name = p.Name ?? string.Empty, ProductCode = p.ProductCode ?? string.Empty }).ToList();
            return Ok(items);
        }

        [HttpPost]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADJUSTMENTS_CREATE)]
        public async Task<IActionResult> Create(CreateAdjustmentRequest request, CancellationToken ct = default)
        {
            if (request.ProductId <= 0 || request.ReasonId <= 0 || request.Quantity <= 0)
            {
                return BadRequest(new { error = "Product, reason, and a positive quantity are required." });
            }

            var reason = await _unitOfWork.Repository<AdjustmentReason>().GetByIdAsync(request.ReasonId, ct);
            if (reason == null || !reason.IsActive) return BadRequest(new { error = "Choose a valid reason." });
            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(request.ProductId, ct);
            if (product == null) return BadRequest(new { error = "Choose a valid product." });

            var locationId = await Mercurius.Services.CurrentLocation.GetIdAsync(_unitOfWork, User);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var now = DateTime.UtcNow;

            var adjustment = new Adjustment
            {
                ProductId = request.ProductId,
                ReasonId = request.ReasonId,
                Quantity = request.Quantity,
                // The (UTC) start of an earlier day the user picked, else now — never the future.
                AdjustmentDate = request.AdjustmentDate is DateTime picked && picked.ToUniversalTime() < now ? picked.ToUniversalTime() : now,
                Note = request.Note ?? string.Empty,
                LocationId = locationId,
                CreatedDate = now,
                CreatedBy = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
                IsActive = true
            };
            await _unitOfWork.Repository<Adjustment>().AddAsync(adjustment, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _adjustmentService.ApplyAsync(adjustment, ct);

            return Ok(new { id = adjustment.Id });
        }
    }
}
