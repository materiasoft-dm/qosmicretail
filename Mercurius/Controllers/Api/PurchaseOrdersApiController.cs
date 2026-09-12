using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Common;
using Mercurius.Shared.PurchaseOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // JSON equivalent of PurchaseOrdersController — mirrors its status state machine exactly:
    // PendingApproval -> Approved -> OrderSent -> ReceivedComplete|ReceivedIncomplete. Each
    // transition endpoint re-validates the current status server-side (never trusts the client),
    // same as the MVC actions.
    [ApiController]
    [Route("api/purchase-orders")]
    [Authorize]
    public class PurchaseOrdersApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public PurchaseOrdersApiController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        [HttpGet]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PURCHASE_ORDERS_LIST)]
        public ActionResult<ListResultDto<PurchaseOrderListItemDto>> Get(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var orders = _unitOfWork.Query<PurchaseOrder>().OrderByDescending(o => o.OrderDate).ToList();
            var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
            var supplierLookup = _unitOfWork.Query<Supplier>().Where(s => supplierIds.Contains(s.Id)).ToDictionary(s => s.Id, s => s.Name ?? string.Empty);

            var items = orders.Select(o => new PurchaseOrderListItemDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                SupplierName = supplierLookup.TryGetValue(o.SupplierId, out var n) ? n : string.Empty,
                Status = o.Status,
                OrderDate = o.OrderDate
            }).ToList();
            return Ok(new ListResultDto<PurchaseOrderListItemDto> { Items = items, Total = items.Count });
        }

        // Mirrors PurchaseOrdersController.Edit — a read-only line-item view in this app (no PO
        // edit form exists; the workflow moves via Approve/MarkSent/MarkReceived instead).
        [HttpGet("{id}")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PURCHASE_ORDERS_LIST)]
        public async Task<ActionResult<PurchaseOrderDetailDto>> GetDetail(int id, CancellationToken ct = default)
        {
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(id, ct);
            if (order == null) return NotFound();

            var supplier = await _unitOfWork.Repository<Supplier>().GetByIdAsync(order.SupplierId, ct);
            var items = (await _unitOfWork.Repository<PurchaseOrderItem>().FindAsync(poi => poi.PurchaseOrderId == order.Id, ct)).ToList();
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            var productsById = productIds.Count == 0
                ? new Dictionary<int, Product>()
                : (await _unitOfWork.Repository<Product>().FindAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);

            return Ok(new PurchaseOrderDetailDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                SupplierName = supplier?.Name ?? string.Empty,
                Status = order.Status,
                OrderDate = order.OrderDate,
                ExpectedDeliveryDate = order.ExpectedDeliveryDate,
                Notes = order.Notes,
                Items = items.Select(i => new PurchaseOrderLineDto
                {
                    ProductName = productsById.TryGetValue(i.ProductId, out var p) ? p.Name ?? string.Empty : "(deleted product)",
                    Quantity = i.Quantity,
                    EstimatedUnitCost = i.EstimatedUnitCost,
                    ReceivedQuantity = i.ReceivedQuantity
                }).ToList()
            });
        }

        [HttpGet("suppliers")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PURCHASE_ORDERS_CREATE)]
        public ActionResult<List<SupplierOptionDto>> GetSuppliers()
        {
            var items = _unitOfWork.Query<Supplier>().Where(s => s.IsActive).OrderBy(s => s.Name)
                .Select(s => new SupplierOptionDto { Id = s.Id, Name = s.Name ?? string.Empty }).ToList();
            return Ok(items);
        }

        [HttpGet("products")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PURCHASE_ORDERS_CREATE)]
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
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PURCHASE_ORDERS_CREATE)]
        public async Task<IActionResult> Create(CreatePurchaseOrderRequest request, CancellationToken ct = default)
        {
            if (request.SupplierId <= 0 || request.Lines.Count == 0 || request.Lines.Any(l => l.ProductId <= 0 || l.Quantity <= 0))
            {
                return BadRequest(new { error = "Supplier and at least one valid product line are required." });
            }

            var order = new PurchaseOrder
            {
                SupplierId = request.SupplierId,
                OrderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                OrderDate = DateTime.UtcNow,
                ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                Notes = request.Notes,
                Status = "PendingApproval",
                CreatedDate = DateTime.UtcNow
            };
            await _unitOfWork.Repository<PurchaseOrder>().AddAsync(order, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            foreach (var line in request.Lines.Where(l => l.ProductId > 0 && l.Quantity > 0))
            {
                await _unitOfWork.Repository<PurchaseOrderItem>().AddAsync(new PurchaseOrderItem
                {
                    PurchaseOrderId = order.Id,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    EstimatedUnitCost = line.EstimatedUnitCost
                }, ct);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new { id = order.Id, orderNumber = order.OrderNumber });
        }

        [HttpPost("{id}/approve")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PURCHASE_ORDERS_APPROVE)]
        public async Task<IActionResult> Approve(int id, CancellationToken ct = default)
        {
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(id, ct);
            if (order == null) return NotFound();
            if (order.Status != "PendingApproval") return BadRequest(new { error = "Order is not pending approval." });

            order.Status = "Approved";
            order.ApprovedDate = DateTime.UtcNow;
            order.UpdatedDate = DateTime.UtcNow;
            await _unitOfWork.Repository<PurchaseOrder>().UpdateAsync(order, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(new { status = order.Status });
        }

        [HttpPost("{id}/mark-sent")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PURCHASE_ORDERS_EDIT)]
        public async Task<IActionResult> MarkSent(int id, CancellationToken ct = default)
        {
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(id, ct);
            if (order == null) return NotFound();
            if (order.Status != "Approved") return BadRequest(new { error = "Order is not approved." });

            order.Status = "OrderSent";
            order.SentDate = DateTime.UtcNow;
            order.UpdatedDate = DateTime.UtcNow;
            await _unitOfWork.Repository<PurchaseOrder>().UpdateAsync(order, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(new { status = order.Status });
        }

        [HttpPost("{id}/mark-received")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PURCHASE_ORDERS_EDIT)]
        public async Task<IActionResult> MarkReceived(int id, [FromQuery] bool isComplete, CancellationToken ct = default)
        {
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(id, ct);
            if (order == null) return NotFound();
            if (order.Status != "OrderSent") return BadRequest(new { error = "Order has not been sent." });

            order.Status = isComplete ? "ReceivedComplete" : "ReceivedIncomplete";
            order.UpdatedDate = DateTime.UtcNow;
            await _unitOfWork.Repository<PurchaseOrder>().UpdateAsync(order, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(new { status = order.Status });
        }
    }
}
