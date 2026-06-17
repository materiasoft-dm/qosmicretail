using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Authorize]
    public class ShipmentController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public ShipmentController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: Shipment
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below. We pass an empty list rather than load
        // every ShipmentArrival on every page hit.
        [Authorize(Policy = Common.ModuleRegistry.Pages.SHIPMENT_LIST)]
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<ShipmentArrival>());
        }

        // GET: Shipment/DataTable
        // Server-side endpoint for jQuery DataTables. Honors the standard request
        // shape and replies with { draw, recordsTotal, recordsFiltered, data: [...] }.
        // Status and supplier names are batch-resolved (no N+1).
        [HttpGet]
        [Authorize(Policy = Common.ModuleRegistry.Pages.SHIPMENT_LIST)]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 0;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            // Column index → ShipmentArrival field. Matches the `columns` array in Index.cshtml.
            // 0 = Id, 1 = Status (sort by StatusId), 2 = Date, 3 = Supplier (sort by SupplierId),
            // 4 = TrackingNumber.
            string sortField = sortColumnIndex switch
            {
                0 => nameof(ShipmentArrival.Id),
                1 => nameof(ShipmentArrival.ShipmentArrivalStatusId),
                2 => nameof(ShipmentArrival.ShipmentArrivalDate),
                3 => nameof(ShipmentArrival.SupplierId),
                4 => nameof(ShipmentArrival.TrackingNumber),
                _ => nameof(ShipmentArrival.ShipmentArrivalDate)
            };
            // Preserve legacy default sort (newest first) when client doesn't specify one.
            if (!q.ContainsKey("order[0][column]"))
            {
                sortField = nameof(ShipmentArrival.ShipmentArrivalDate);
                sortDir = "desc";
            }

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var collection = _unitOfWork.GetCollection<ShipmentArrival>();

            var recordsTotal = collection.Count();

            var query = collection.Query();
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                // Searchable: TrackingNumber and Notes. Supplier name would require a
                // join we can't do in LiteDB's expression engine without first fetching
                // supplier IDs — skip it; users searching by supplier can filter via the column.
                query = query.Where(sa =>
                    (sa.TrackingNumber != null && sa.TrackingNumber.ToLower().Contains(s)) ||
                    (sa.Notes != null && sa.Notes.ToLower().Contains(s)));
            }

            var recordsFiltered = query.Count();

            var bsonField = LiteDB.BsonExpression.Create($"$.{sortField}");
            query = sortDir == "desc"
                ? query.OrderByDescending(bsonField)
                : query.OrderBy(bsonField);

            var pageItems = query.Skip(start).Limit(length).ToList();

            // Batch-resolve status names + supplier names (avoids N+1).
            var statusIds = pageItems.Select(s => s.ShipmentArrivalStatusId).Distinct().ToList();
            var statusLookup = statusIds.Count == 0
                ? new Dictionary<int, ShipmentArrivalStatus>()
                : _unitOfWork.GetCollection<ShipmentArrivalStatus>()
                    .Find(st => statusIds.Contains(st.Id))
                    .ToDictionary(st => st.Id, st => st);

            var supplierIds = pageItems.Where(s => s.SupplierId.HasValue)
                .Select(s => s.SupplierId!.Value).Distinct().ToList();
            var supplierLookup = supplierIds.Count == 0
                ? new Dictionary<int, string>()
                : _unitOfWork.GetCollection<Supplier>()
                    .Find(sp => supplierIds.Contains(sp.Id))
                    .ToDictionary(sp => sp.Id, sp => sp.Name ?? string.Empty);

            var data = pageItems.Select(sa =>
            {
                ShipmentArrivalStatus? status = null;
                statusLookup.TryGetValue(sa.ShipmentArrivalStatusId, out status);
                string supplierName = string.Empty;
                if (sa.SupplierId.HasValue)
                {
                    supplierLookup.TryGetValue(sa.SupplierId.Value, out supplierName!);
                }
                return new
                {
                    id = sa.Id,
                    statusName = status?.Name ?? string.Empty,
                    statusCss = status?.CssClass ?? string.Empty,
                    date = sa.ShipmentArrivalDate,
                    supplierName = supplierName ?? string.Empty,
                    trackingNumber = sa.TrackingNumber ?? string.Empty
                };
            }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        // Populates ViewBag.SupplierId and ViewBag.PurchaseOrderId for the Create form.
        // NOTE: the old code accidentally had [Authorize] on this private helper instead of
        // on Index(); the attribute had no effect there. Index/DataTable are now properly
        // gated with SHIPMENT_LIST and Create/POST with SHIPMENT_CREATE.
        private async Task LoadShipmentViewData(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var suppliers = await _unitOfWork.Repository<Supplier>().GetAllAsync(ct);
            ViewBag.SupplierId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(suppliers.Where(s => s.IsActive).OrderBy(s => s.Name), "Id", "Name");

            var pos = await _unitOfWork.Repository<PurchaseOrder>().GetAllAsync(ct);
            ViewBag.PurchaseOrderId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                pos.Where(po => po.Status == "Approved" || po.Status == "OrderSent").OrderByDescending(po => po.OrderDate),
                "Id", "OrderNumber");
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.SHIPMENT_CREATE)]
        public async Task<IActionResult> Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await LoadShipmentViewData(ct);
            return View();
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.SHIPMENT_CREATE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShipmentArrival shipment, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                shipment.CreatedDate = DateTime.UtcNow;
                await _unitOfWork.Repository<ShipmentArrival>().AddAsync(shipment, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(shipment);
        }

        /// <summary>Returns supplier info for a purchase order (used by AJAX on shipment form).</summary>
        [HttpGet]
        public async Task<IActionResult> GetSupplierForPo(int poId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var po = await _unitOfWork.Repository<PurchaseOrder>().GetByIdAsync(poId, ct);
            if (po == null) return NotFound();
            var supplier = await _unitOfWork.Repository<Supplier>().GetByIdAsync(po.SupplierId, ct);
            return Json(new { supplierId = po.SupplierId, supplierName = supplier?.Name ?? "Unknown" });
        }
    }
}
