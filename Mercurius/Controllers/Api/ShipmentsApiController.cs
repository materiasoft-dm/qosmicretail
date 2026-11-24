using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Common;
using Mercurius.Shared.Shipments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // Shipments received against purchase orders. PO options are limited to orders that can still be
    // received against; a new shipment defaults to the "Pending" status (Id 1, per Program.cs's seeded
    // ShipmentArrivalStatuses) and to today's date. A shipment recorded as Received (Id 2) is what puts
    // stock on the shelf — see StockReceiving.
    [ApiController]
    [Route("api/shipments")]
    [Authorize]
    public class ShipmentsApiController : ControllerBase
    {
        private const int PendingStatusId = 1;
        private const int ReceivedStatusId = 2;
        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.StockReceiving _stockReceiving;

        public ShipmentsApiController(IUnitOfWork unitOfWork, Mercurius.Services.StockReceiving stockReceiving)
        {
            _unitOfWork = unitOfWork;
            _stockReceiving = stockReceiving;
        }

        [HttpGet]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.SHIPMENT_LIST)]
        public ActionResult<ListResultDto<ShipmentListItemDto>> Get(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var shipments = _unitOfWork.Query<ShipmentArrival>().OrderByDescending(s => s.ShipmentArrivalDate).ToList();

            var statusIds = shipments.Select(s => s.ShipmentArrivalStatusId).Distinct().ToList();
            var statusLookup = _unitOfWork.Query<ShipmentArrivalStatus>().Where(s => statusIds.Contains(s.Id)).ToDictionary(s => s.Id, s => s.Name ?? string.Empty);

            var supplierIds = shipments.Where(s => s.SupplierId.HasValue).Select(s => s.SupplierId!.Value).Distinct().ToList();
            var supplierLookup = _unitOfWork.Query<Supplier>().Where(s => supplierIds.Contains(s.Id)).ToDictionary(s => s.Id, s => s.Name ?? string.Empty);

            var shipmentIds = shipments.Select(s => s.Id).ToList();
            var unitsByShipment = _unitOfWork.Query<ShipmentArrivalItem>().Where(i => shipmentIds.Contains(i.ShipmentArrivalId))
                .GroupBy(i => i.ShipmentArrivalId).Select(g => new { g.Key, Units = g.Sum(i => i.ItemCount) })
                .ToDictionary(x => x.Key, x => x.Units);

            var items = shipments.Select(s => new ShipmentListItemDto
            {
                UnitsReceived = unitsByShipment.TryGetValue(s.Id, out var units) ? units : 0m,
                Id = s.Id,
                StatusName = statusLookup.TryGetValue(s.ShipmentArrivalStatusId, out var sn) ? sn : "Unknown",
                Date = s.ShipmentArrivalDate,
                SupplierName = s.SupplierId.HasValue && supplierLookup.TryGetValue(s.SupplierId.Value, out var n) ? n : string.Empty,
                TrackingNumber = s.TrackingNumber ?? string.Empty
            }).ToList();
            return Ok(new ListResultDto<ShipmentListItemDto> { Items = items, Total = items.Count });
        }

        [HttpGet("suppliers")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.SHIPMENT_CREATE)]
        public ActionResult<List<SupplierOptionDto>> GetSuppliers()
        {
            var items = _unitOfWork.Query<Supplier>().Where(s => s.IsActive).OrderBy(s => s.Name)
                .Select(s => new SupplierOptionDto { Id = s.Id, Name = s.Name ?? string.Empty }).ToList();
            return Ok(items);
        }

        [HttpGet("purchase-orders")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.SHIPMENT_CREATE)]
        public ActionResult<List<PurchaseOrderOptionDto>> GetPurchaseOrders()
        {
            var orders = _unitOfWork.Query<PurchaseOrder>()
                .Where(po => Mercurius.Services.StockReceiving.ReceivablePurchaseOrderStatuses.Contains(po.Status))
                .OrderByDescending(po => po.OrderDate).ToList();
            var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
            var supplierLookup = _unitOfWork.Query<Supplier>().Where(s => supplierIds.Contains(s.Id)).ToDictionary(s => s.Id, s => s.Name ?? string.Empty);

            var items = orders.Select(o => new PurchaseOrderOptionDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                SupplierId = o.SupplierId,
                SupplierName = supplierLookup.TryGetValue(o.SupplierId, out var n) ? n : string.Empty
            }).ToList();
            return Ok(items);
        }

        [HttpGet("statuses")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.SHIPMENT_CREATE)]
        public ActionResult<List<ShipmentStatusDto>> GetStatuses() =>
            Ok(_unitOfWork.Query<ShipmentArrivalStatus>().Where(s => s.IsActive).OrderBy(s => s.Id)
                .Select(s => new ShipmentStatusDto { Id = s.Id, Name = s.Name ?? string.Empty }).ToList());

        // What a purchase order still expects: its lines, with what's already arrived, ready to be
        // filled in with what the delivery actually holds. Prices start at the order's estimate and
        // the product's current sale price.
        [HttpGet("purchase-orders/{id:int}/lines")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.SHIPMENT_CREATE)]
        public async Task<ActionResult<List<ReceivableLineDto>>> GetPurchaseOrderLines(int id, CancellationToken ct = default)
        {
            var order = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(id, ct);
            if (order == null) return NotFound();

            var lines = _unitOfWork.Query<PurchaseOrderItem>().Where(l => l.PurchaseOrderId == id).ToList();
            var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
            var products = _unitOfWork.Query<Product>().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id);

            return Ok(lines.Where(l => products.ContainsKey(l.ProductId)).Select(l =>
            {
                var product = products[l.ProductId];
                return new ReceivableLineDto
                {
                    ProductId = product.Id,
                    ProductName = product.Name ?? string.Empty,
                    ProductCode = product.ProductCode ?? string.Empty,
                    Ordered = l.Quantity,
                    AlreadyReceived = l.ReceivedQuantity ?? 0m,
                    UnitCost = l.EstimatedUnitCost ?? product.CurrentCostPrice ?? 0m,
                    UnitSalePrice = product.CurrentSalePrice ?? 0m
                };
            }).ToList());
        }

        // Products to add to a delivery that isn't against a purchase order (or isn't on it).
        [HttpGet("products")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.SHIPMENT_CREATE)]
        public ActionResult<List<ReceivableProductDto>> SearchProducts(string? search = null)
        {
            var query = _unitOfWork.Query<Product>().Where(p => p.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(p => (p.Name != null && p.Name.ToLower().Contains(s)) || (p.ProductCode != null && p.ProductCode.ToLower().Contains(s))
                    || (p.Barcode != null && p.Barcode.ToLower().Contains(s)));
            }
            return Ok(query.OrderBy(p => p.Name).Take(30).Select(p => new ReceivableProductDto
            {
                Id = p.Id,
                Name = p.Name ?? string.Empty,
                ProductCode = p.ProductCode ?? string.Empty,
                UnitCost = p.CurrentCostPrice ?? 0m,
                UnitSalePrice = p.CurrentSalePrice ?? 0m
            }).ToList());
        }

        [HttpPost]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.SHIPMENT_CREATE)]
        public Task<IActionResult> Create(CreateShipmentRequest request, CancellationToken ct = default) =>
            Mercurius.Services.ConcurrencyRetry.RunAsync(_unitOfWork, () => CreateShipmentAsync(request, ct), ct);

        private async Task<IActionResult> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken ct)
        {
            // The same form submitted twice (a double click, a retry after a lost response) is one shipment.
            if (request.SyncId is Guid syncId && syncId != Guid.Empty)
            {
                var existing = (await _unitOfWork.Repository<ShipmentArrival>().FindAsync(s => s.SyncId == syncId, ct)).FirstOrDefault();
                if (existing != null) return Ok(new { id = existing.Id });
            }

            var locationId = await Mercurius.Services.CurrentLocation.GetIdAsync(_unitOfWork, User);

            var statusId = request.StatusId ?? PendingStatusId;
            if (!_unitOfWork.Query<ShipmentArrivalStatus>().Any(s => s.Id == statusId))
            {
                return BadRequest(new { error = "Choose a valid status." });
            }

            // Stock arrives with a shipment recorded as Received, and only with one: a pending, delayed or
            // damaged shipment has put nothing on the shelf. Recording "Received" without saying what
            // arrived used to be allowed, and quietly changed no stock.
            var items = request.Items ?? new List<ShipmentItemRequest>();
            var receiving = statusId == ReceivedStatusId;
            if (!receiving && items.Count > 0)
            {
                return BadRequest(new { error = "Only a shipment with the status Received can add stock. Change the status, or remove the items." });
            }
            if (receiving)
            {
                var problem = Mercurius.Services.StockReceiving.Validate(items);
                if (problem != null) return BadRequest(new { error = problem });
            }

            PurchaseOrder? purchaseOrder = null;
            if (request.PurchaseOrderId is int poId)
            {
                purchaseOrder = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(poId, ct);
                if (purchaseOrder == null) return BadRequest(new { error = "That purchase order wasn't found." });
                if (receiving && !Mercurius.Services.StockReceiving.ReceivablePurchaseOrderStatuses.Contains(purchaseOrder.Status))
                {
                    return BadRequest(new { error = "That purchase order can't be received against: it isn't approved yet, or has already been received in full." });
                }
            }

            var arrivedAt = request.ArrivalDate?.ToUniversalTime() ?? DateTime.UtcNow;
            var shipment = new ShipmentArrival
            {
                SyncId = request.SyncId is Guid sid && sid != Guid.Empty ? sid : null,
                SupplierId = request.SupplierId,
                PurchaseOrderId = request.PurchaseOrderId,
                TrackingNumber = request.TrackingNumber ?? string.Empty,
                Notes = request.Notes ?? string.Empty,
                ShipmentArrivalStatusId = statusId,
                // The (UTC) start of the day the user picked, else now.
                ShipmentArrivalDate = arrivedAt,
                LocationId = locationId,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };

            // One transaction: the shipment, its lines, the stock and batches, and the purchase order
            // moving on either all happen or none do.
            await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                await _unitOfWork.Repository<ShipmentArrival>().AddAsync(shipment, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                if (receiving)
                {
                    var error = await _stockReceiving.ReceiveAsync(_unitOfWork, shipment, purchaseOrder, items, arrivedAt, ct);
                    if (error != null)
                    {
                        await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                        return BadRequest(new { error });
                    }
                }
                await _unitOfWork.CommitTransactionAsync(ct);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }
            return Ok(new { id = shipment.Id });
        }
    }
}
