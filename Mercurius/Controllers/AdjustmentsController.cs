using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Authorize]
    public class AdjustmentsController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.AdjustmentService _adjustmentService;

        public AdjustmentsController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork, Mercurius.Services.AdjustmentService adjustmentService)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _adjustmentService = adjustmentService;
        }

        // GET: Adjustments
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below. We pass an empty list rather than load
        // every Adjustment on every page hit.
        [Authorize(Policy = Common.ModuleRegistry.Pages.ADJUSTMENTS_INDEX)]
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<Adjustment>());
        }

        // GET: Adjustments/DataTable
        // Server-side endpoint for jQuery DataTables. Honors the standard request
        // shape (draw / start / length / order[0][column] / order[0][dir] / search[value])
        // and replies with { draw, recordsTotal, recordsFiltered, data: [...] }.
        // All filtering/ordering/paging is pushed down to the database so the browser
        // only ever receives one page of rows.
        [HttpGet]
        [Authorize(Policy = Common.ModuleRegistry.Pages.ADJUSTMENTS_INDEX)]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 0;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            // Preserve the legacy default sort (most recent first) when the client doesn't specify one.
            if (!q.ContainsKey("order[0][column]"))
            {
                sortColumnIndex = 1;
                sortDir = "desc";
            }

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var collection = _unitOfWork.Query<Adjustment>();

            var recordsTotal = collection.Count();

            var query = collection;
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                // Searchable text is just the Note field; ReasonId/Quantity are numeric
                // and the user already sees them rendered.
                query = query.Where(a => a.Note != null && a.Note.ToLower().Contains(s));
            }

            var recordsFiltered = query.Count();

            // Column index → Adjustment field. Matches the `columns` array in Index.cshtml.
            // 0 = Id, 1 = AdjustmentDate, 2 = Reason (joined, sort by ReasonId), 3 = Quantity.
            // Reason is a lookup so we sort by ReasonId rather than the resolved name —
            // sorting by name would require pulling the full reason list every request.
            bool desc = sortDir == "desc";
            query = (sortColumnIndex, desc) switch
            {
                (0, true) => query.OrderByDescending(a => a.Id),
                (0, false) => query.OrderBy(a => a.Id),
                (2, true) => query.OrderByDescending(a => a.ReasonId),
                (2, false) => query.OrderBy(a => a.ReasonId),
                (3, true) => query.OrderByDescending(a => a.Quantity),
                (3, false) => query.OrderBy(a => a.Quantity),
                (_, true) => query.OrderByDescending(a => a.AdjustmentDate),
                (_, false) => query.OrderBy(a => a.AdjustmentDate)
            };

            var pageItems = query.Skip(start).Take(length).ToList();

            // Batch-resolve reason names (avoids N+1). Same shape as Products → Category.
            var reasonIds = pageItems.Select(a => a.ReasonId).Distinct().ToList();
            var reasonLookup = reasonIds.Count == 0
                ? new Dictionary<int, AdjustmentReason>()
                : _unitOfWork.Query<AdjustmentReason>()
                    .Where(r => reasonIds.Contains(r.Id))
                    .ToDictionary(r => r.Id, r => r);

            // Batch-resolve the invoice a refund-driven adjustment came from (if any), so the
            // view can link back to /Invoices/Details/{invoiceId} — see InvoiceItemRefund.
            var refundLineIds = pageItems.Where(a => a.InvoiceItemRefundId.HasValue)
                .Select(a => a.InvoiceItemRefundId!.Value).Distinct().ToList();
            var invoiceIdByRefundLineId = refundLineIds.Count == 0
                ? new Dictionary<int, int>()
                : _unitOfWork.Query<InvoiceItemRefund>()
                    .Where(r => refundLineIds.Contains(r.Id))
                    .ToDictionary(r => r.Id, r => r.InvoiceId);

            var data = pageItems.Select(a =>
            {
                AdjustmentReason? reason = null;
                reasonLookup.TryGetValue(a.ReasonId, out reason);
                int? invoiceId = a.InvoiceItemRefundId.HasValue && invoiceIdByRefundLineId.TryGetValue(a.InvoiceItemRefundId.Value, out var invId)
                    ? invId
                    : null;
                return new
                {
                    id = a.Id,
                    date = a.AdjustmentDate, // ISO 8601 over JSON; the view formats it
                    reasonName = reason?.Name ?? string.Empty,
                    reasonCss = reason?.CssClass ?? string.Empty,
                    quantity = a.Quantity,
                    invoiceId
                };
            }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.ADJUSTMENTS_CREATE)]
        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.ADJUSTMENTS_CREATE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Adjustment adjustment, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                adjustment.CreatedDate = DateTime.UtcNow;
                await _unitOfWork.Repository<Adjustment>().AddAsync(adjustment, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                await _adjustmentService.ApplyAsync(adjustment, ct);
                return RedirectToAction(nameof(Index));
            }
            return View(adjustment);
        }
    }
}
