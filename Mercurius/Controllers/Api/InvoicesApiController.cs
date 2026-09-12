using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // JSON equivalent of InvoiceListController.DataTable — mirrors its customer-name batch
    // lookup and enum-derived statusName exactly, and applies no IsActive/status exclusion
    // (matching that controller: even "Deleted"-status invoices still list, status is purely
    // a display concern there).
    // Route is "api/invoice-list", not "api/invoices" — Controllers/InvoicesController.cs
    // already owns that route (a pre-existing, differently-shaped API for other consumers);
    // this is a separate endpoint purpose-built for the Blazor Invoices page, mirroring
    // InvoiceListController.DataTable instead.
    [ApiController]
    [Route("api/invoice-list")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.INVOICE_INDEX)]
    public class InvoicesApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.AdjustmentService _adjustmentService;

        public InvoicesApiController(IUnitOfWork unitOfWork, Mercurius.Services.AdjustmentService adjustmentService)
        {
            _unitOfWork = unitOfWork;
            _adjustmentService = adjustmentService;
        }

        [HttpGet]
        public ActionResult<InvoiceListResultDto> Get(string? search = null, int page = 1, int pageSize = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200;

            var query = _unitOfWork.Query<Invoice>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                var matchingCustomerIds = _unitOfWork.Query<Customer>()
                    .Where(c => (c.FirstName != null && c.FirstName.ToLower().Contains(s))
                             || (c.LastName != null && c.LastName.ToLower().Contains(s)))
                    .Select(c => c.Id).ToList();

                query = matchingCustomerIds.Count > 0
                    ? query.Where(i => (i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(s))
                                     || (i.CustomerId.HasValue && matchingCustomerIds.Contains(i.CustomerId.Value)))
                    : query.Where(i => i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(s));
            }

            var total = query.Count();
            var pageItems = query.OrderByDescending(i => i.InvoiceDate)
                .Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var customerIds = pageItems.Where(i => i.CustomerId.HasValue).Select(i => i.CustomerId!.Value).Distinct().ToList();
            var customerLookup = customerIds.Count == 0
                ? new Dictionary<int, string>()
                : _unitOfWork.Query<Customer>().Where(c => customerIds.Contains(c.Id))
                    .ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim());

            var items = pageItems.Select(i =>
            {
                string customerName = string.Empty;
                if (i.CustomerId.HasValue) customerLookup.TryGetValue(i.CustomerId.Value, out customerName!);
                var statusName = Enum.IsDefined(typeof(StatusCollection.InvoiceStatus), i.StatusId)
                    ? ((StatusCollection.InvoiceStatus)i.StatusId).ToString()
                    : "Unknown";
                return new InvoiceListItemDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber ?? string.Empty,
                    InvoiceDate = i.InvoiceDate,
                    InvoiceDueDate = i.InvoiceDueDate,
                    CustomerName = customerName ?? string.Empty,
                    StatusId = i.StatusId,
                    StatusName = statusName,
                    PaidAmount = i.PaidAmount,
                    HasRefund = i.HasRefund
                };
            }).ToList();

            return Ok(new InvoiceListResultDto { Items = items, Total = total });
        }

        // Mirrors InvoiceListController.Details exactly — see that action's own comments for
        // the batch-resolve reasoning (no N+1 for refund reasons/headers).
        [HttpGet("{id}")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.INVOICE_VIEW)]
        public async Task<ActionResult<InvoiceDetailDto>> GetDetail(int id, CancellationToken ct = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>().GetByIdAsync(id, ct);
            if (invoice == null) return NotFound();

            string customerName = string.Empty;
            if (invoice.CustomerId.HasValue)
            {
                var customer = await _unitOfWork.Repository<Customer>().GetByIdAsync(invoice.CustomerId.Value, ct);
                customerName = customer == null ? string.Empty : $"{customer.FirstName} {customer.LastName}".Trim();
            }
            var statusName = Enum.IsDefined(typeof(StatusCollection.InvoiceStatus), invoice.StatusId)
                ? ((StatusCollection.InvoiceStatus)invoice.StatusId).ToString() : "Unknown";

            var items = (await _unitOfWork.Repository<InvoiceItem>().FindAsync(ii => ii.InvoiceId == id, ct)).ToList();
            var productIds = items.Select(ii => ii.ProductId).Distinct().ToList();
            var productsById = (await _unitOfWork.Repository<Product>().FindAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);

            var itemIds = items.Select(ii => ii.Id).ToList();
            var refundLines = itemIds.Count == 0 ? new List<InvoiceItemRefund>() : (await _unitOfWork.Repository<InvoiceItemRefund>().FindAsync(r => itemIds.Contains(r.InvoiceItemId), ct)).ToList();
            var refundedQtyByItemId = refundLines.GroupBy(r => r.InvoiceItemId).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

            var itemRows = items.Select(ii =>
            {
                productsById.TryGetValue(ii.ProductId, out var product);
                refundedQtyByItemId.TryGetValue(ii.Id, out var refundedQty);
                return new InvoiceItemRowDto
                {
                    InvoiceItemId = ii.Id,
                    ProductName = product?.Name ?? "(deleted product)",
                    ProductCode = product?.ProductCode ?? string.Empty,
                    QuantitySold = ii.Quantity,
                    QuantityRefunded = refundedQty,
                    SalePrice = ii.SalePrice
                };
            }).ToList();

            var reasonIds = refundLines.Select(r => r.RefundReasonId).Distinct().ToList();
            var reasonsById = reasonIds.Count == 0 ? new Dictionary<int, RefundReason>() : (await _unitOfWork.Repository<RefundReason>().FindAsync(r => reasonIds.Contains(r.Id), ct)).ToDictionary(r => r.Id);
            var refundHeaderIds = refundLines.Select(r => r.InvoiceRefundId).Distinct().ToList();
            var refundHeadersById = refundHeaderIds.Count == 0 ? new Dictionary<int, InvoiceRefund>() : (await _unitOfWork.Repository<InvoiceRefund>().FindAsync(r => refundHeaderIds.Contains(r.Id), ct)).ToDictionary(r => r.Id);

            var refundHistory = refundLines.OrderByDescending(r => r.DateRefunded).Select(r =>
            {
                productsById.TryGetValue(r.ProductId, out var product);
                reasonsById.TryGetValue(r.RefundReasonId, out var reason);
                refundHeadersById.TryGetValue(r.InvoiceRefundId, out var header);
                return new RefundHistoryRowDto
                {
                    RefundNumber = header?.RefundNumber ?? string.Empty,
                    DateRefunded = r.DateRefunded,
                    ProductName = product?.Name ?? "(deleted product)",
                    Quantity = r.Quantity,
                    ReasonName = reason?.Name ?? "(deleted reason)",
                    Remarks = r.Remarks,
                    WasRestocked = r.WasRestocked
                };
            }).ToList();

            var activeReasons = (await _unitOfWork.Repository<RefundReason>().FindAsync(r => r.IsActive, ct))
                .OrderBy(r => r.Name)
                .Select(r => new RefundReasonOptionDto { Id = r.Id, Name = r.Name ?? string.Empty })
                .ToList();

            return Ok(new InvoiceDetailDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                InvoiceDate = invoice.InvoiceDate,
                CustomerName = customerName,
                StatusName = statusName,
                PaidAmount = invoice.PaidAmount,
                Items = itemRows,
                RefundHistory = refundHistory,
                ActiveRefundReasons = activeReasons
            });
        }

        // Mirrors RefundsController.Create exactly — see that action's own comments (never trust
        // a client-computed max refundable quantity; the "Refund" AdjustmentReason is looked up
        // or created on demand; invoice flips to Refunded only once every line is fully refunded).
        [HttpPost("refund")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.REFUND_CREATE)]
        public async Task<IActionResult> CreateRefund(CreateRefundRequest request, CancellationToken ct = default)
        {
            var item = await _unitOfWork.Repository<InvoiceItem>().GetByIdAsync(request.InvoiceItemId, ct);
            if (item == null) return NotFound(new { error = "Invoice item not found." });

            var reason = await _unitOfWork.Repository<RefundReason>().GetByIdAsync(request.RefundReasonId, ct);
            if (reason == null || !reason.IsActive) return BadRequest(new { error = "Select a valid refund reason." });

            var existingRefunds = await _unitOfWork.Repository<InvoiceItemRefund>().FindAsync(r => r.InvoiceItemId == request.InvoiceItemId, ct);
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
            var allItems = await _unitOfWork.Repository<InvoiceItem>().FindAsync(ii => ii.InvoiceId == invoice.Id, ct);
            var allItemIds = allItems.Select(ii => ii.Id).ToList();
            var allRefundLines = await _unitOfWork.Repository<InvoiceItemRefund>().FindAsync(r => allItemIds.Contains(r.InvoiceItemId), ct);
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

        private async Task<AdjustmentReason> GetOrCreateRefundAdjustmentReasonAsync(CancellationToken ct)
        {
            var existing = await _unitOfWork.Repository<AdjustmentReason>().FindAsync(r => r.Name == "Refund", ct);
            var found = existing.FirstOrDefault();
            if (found != null) return found;

            var created = new AdjustmentReason { Name = "Refund", Description = "Stock restored from a customer refund.", IsActive = true, IsInbound = true };
            await _unitOfWork.Repository<AdjustmentReason>().AddAsync(created, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return created;
        }
    }
}
