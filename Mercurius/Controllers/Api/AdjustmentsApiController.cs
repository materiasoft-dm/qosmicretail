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
    // JSON equivalent of AdjustmentsController — the actual stock-mutation side effect
    // (AdjustmentService.ApplyAsync) stays server-side here exactly as in the MVC controller;
    // Blazor only ever sees the resulting list, never re-implements the stock math itself.
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

        [HttpGet]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADJUSTMENTS_INDEX)]
        public ActionResult<ListResultDto<AdjustmentListItemDto>> Get(string? search = null, int page = 1, int pageSize = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200;

            var query = _unitOfWork.Query<Adjustment>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(a => a.Note != null && a.Note.ToLower().Contains(s));
            }

            var total = query.Count();
            var pageItems = query.OrderByDescending(a => a.AdjustmentDate)
                .Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var reasonIds = pageItems.Select(a => a.ReasonId).Distinct().ToList();
            var reasonLookup = _unitOfWork.Query<AdjustmentReason>().Where(r => reasonIds.Contains(r.Id)).ToDictionary(r => r.Id, r => r.Name ?? string.Empty);

            var refundIds = pageItems.Where(a => a.InvoiceItemRefundId.HasValue).Select(a => a.InvoiceItemRefundId!.Value).Distinct().ToList();
            var invoiceIdLookup = refundIds.Count == 0
                ? new Dictionary<int, int>()
                : _unitOfWork.Query<InvoiceItemRefund>().Where(r => refundIds.Contains(r.Id)).ToDictionary(r => r.Id, r => r.InvoiceId);

            var items = pageItems.Select(a => new AdjustmentListItemDto
            {
                Id = a.Id,
                Date = a.AdjustmentDate,
                ReasonName = reasonLookup.TryGetValue(a.ReasonId, out var rn) ? rn : string.Empty,
                Quantity = a.Quantity,
                InvoiceId = a.InvoiceItemRefundId.HasValue && invoiceIdLookup.TryGetValue(a.InvoiceItemRefundId.Value, out var invId) ? invId : null
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

            var locationId = await Mercurius.ViewComponents.Dashboard.DashboardLocationContext.GetCurrentLocationIdAsync(_unitOfWork, User);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var adjustment = new Adjustment
            {
                ProductId = request.ProductId,
                ReasonId = request.ReasonId,
                Quantity = request.Quantity,
                Note = request.Note ?? string.Empty,
                LocationId = locationId,
                CreatedDate = DateTime.UtcNow,
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
