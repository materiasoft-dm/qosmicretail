using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.REFUND_CREATE)]
    public class RefundsController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Services.AdjustmentService _adjustmentService;

        public RefundsController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork, Services.AdjustmentService adjustmentService)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _adjustmentService = adjustmentService;
        }

        public class RefundRequest
        {
            public int InvoiceItemId { get; set; }
            public decimal Quantity { get; set; }
            public int RefundReasonId { get; set; }
            public string? Notes { get; set; }
            public bool Restock { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RefundRequest request, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var item = await _unitOfWork.Repository<InvoiceItem>().GetByIdAsync(request.InvoiceItemId, ct);
            if (item == null) return NotFound(new { error = "Invoice item not found." });

            var reason = await _unitOfWork.Repository<RefundReason>().GetByIdAsync(request.RefundReasonId, ct);
            if (reason == null || !reason.IsActive) return BadRequest(new { error = "Select a valid refund reason." });

            // Never trust a client-computed max — re-derive the remaining refundable quantity
            // from what's actually been refunded so far.
            var existingRefunds = await _unitOfWork.Repository<InvoiceItemRefund>()
                .FindAsync(r => r.InvoiceItemId == request.InvoiceItemId, ct);
            var alreadyRefunded = existingRefunds.Sum(r => r.Quantity);
            var remaining = item.Quantity - alreadyRefunded;

            if (request.Quantity <= 0 || request.Quantity > remaining)
            {
                return BadRequest(new { error = $"Quantity must be greater than 0 and no more than {remaining:0.##}." });
            }

            var invoice = await _unitOfWork.Repository<Invoice>().GetByIdAsync(item.InvoiceId, ct);
            if (invoice == null) return NotFound(new { error = "Invoice not found." });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var currentUserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty;
            var now = DateTime.UtcNow;

            var refundHeader = new InvoiceRefund
            {
                RefundNumber = $"REF-{now:yyyyMMddHHmmssfff}",
                DateRefunded = now,
                RefundedByUserId = currentUserId,
                InvoiceId = invoice.Id,
                CreatedDate = now,
                CreatedBy = currentUserId,
                Notes = request.Notes ?? string.Empty,
                IsActive = true
            };
            await _unitOfWork.Repository<InvoiceRefund>().AddAsync(refundHeader, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var refundLine = new InvoiceItemRefund
            {
                InvoiceItemId = item.Id,
                InvoiceId = invoice.Id,
                ProductId = item.ProductId,
                DateRefunded = now,
                Quantity = request.Quantity,
                Remarks = request.Notes,
                InvoiceRefundId = refundHeader.Id,
                RefundReasonId = request.RefundReasonId,
                WasRestocked = request.Restock
            };
            await _unitOfWork.Repository<InvoiceItemRefund>().AddAsync(refundLine, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            if (request.Restock)
            {
                var refundStockReason = await GetOrCreateRefundAdjustmentReasonAsync(ct);
                var adjustment = new Adjustment
                {
                    AdjustmentDate = now,
                    ReasonId = refundStockReason.Id,
                    Quantity = request.Quantity,
                    ProductId = item.ProductId,
                    LocationId = invoice.LocationId,
                    Note = $"Restocked from refund {refundHeader.RefundNumber}",
                    CreatedBy = currentUserId,
                    CreatedDate = now,
                    IsActive = true,
                    InvoiceItemRefundId = refundLine.Id
                };
                await _unitOfWork.Repository<Adjustment>().AddAsync(adjustment, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                await _adjustmentService.ApplyAsync(adjustment, ct);
            }

            invoice.HasRefund = true;

            // If every item on this invoice is now fully refunded, the invoice itself is fully
            // refunded — flip its status. A partial refund leaves the status as-is.
            var allItems = await _unitOfWork.Repository<InvoiceItem>().FindAsync(ii => ii.InvoiceId == invoice.Id, ct);
            var allItemIds = allItems.Select(ii => ii.Id).ToList();
            var allRefundLines = await _unitOfWork.Repository<InvoiceItemRefund>()
                .FindAsync(r => allItemIds.Contains(r.InvoiceItemId), ct);
            var refundedByItemId = allRefundLines.GroupBy(r => r.InvoiceItemId).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));
            var fullyRefunded = allItems.All(ii => refundedByItemId.TryGetValue(ii.Id, out var qty) && qty >= ii.Quantity);
            if (fullyRefunded)
            {
                invoice.StatusId = (int)StatusCollection.InvoiceStatus.Refunded;
            }

            await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new { success = true });
        }

        // "Refund" is a fixed, system-level Adjustment reason distinct from the user-editable
        // RefundReason list (which answers "why is the customer returning it", not "why did
        // stock change") — looked up by name on demand rather than seeded at startup, so this
        // feature needs no Program.cs change or seed-order dependency.
        private async Task<AdjustmentReason> GetOrCreateRefundAdjustmentReasonAsync(CancellationToken ct)
        {
            var existing = await _unitOfWork.Repository<AdjustmentReason>().FindAsync(r => r.Name == "Refund", ct);
            var found = existing.FirstOrDefault();
            if (found != null) return found;

            var created = new AdjustmentReason
            {
                Name = "Refund",
                Description = "Stock restored from a customer refund.",
                IsActive = true,
                IsInbound = true
            };
            await _unitOfWork.Repository<AdjustmentReason>().AddAsync(created, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return created;
        }
    }
}
