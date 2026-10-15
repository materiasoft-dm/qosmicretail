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
    // Shipments received against purchase orders. PO options are limited to Approved/OrderSent
    // orders; a new shipment defaults to the "Pending" status (Id 1, per Program.cs's seeded
    // ShipmentArrivalStatuses) and to today's date.
    [ApiController]
    [Route("api/shipments")]
    [Authorize]
    public class ShipmentsApiController : ControllerBase
    {
        private const int PendingStatusId = 1;
        private readonly IUnitOfWork _unitOfWork;
        public ShipmentsApiController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

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

            var items = shipments.Select(s => new ShipmentListItemDto
            {
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
                .Where(po => po.Status == "Approved" || po.Status == "OrderSent")
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

        [HttpPost]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.SHIPMENT_CREATE)]
        public async Task<IActionResult> Create(CreateShipmentRequest request, CancellationToken ct = default)
        {
            var locationId = await Mercurius.Services.CurrentLocation.GetIdAsync(_unitOfWork, User);

            var statusId = request.StatusId ?? PendingStatusId;
            if (!_unitOfWork.Query<ShipmentArrivalStatus>().Any(s => s.Id == statusId))
            {
                return BadRequest(new { error = "Choose a valid status." });
            }

            var shipment = new ShipmentArrival
            {
                SupplierId = request.SupplierId,
                PurchaseOrderId = request.PurchaseOrderId,
                TrackingNumber = request.TrackingNumber ?? string.Empty,
                Notes = request.Notes ?? string.Empty,
                ShipmentArrivalStatusId = statusId,
                // The (UTC) start of the day the user picked, else now.
                ShipmentArrivalDate = request.ArrivalDate?.ToUniversalTime() ?? DateTime.UtcNow,
                LocationId = locationId,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };
            await _unitOfWork.Repository<ShipmentArrival>().AddAsync(shipment, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(new { id = shipment.Id });
        }
    }
}
