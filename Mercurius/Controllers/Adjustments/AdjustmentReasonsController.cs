using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers.Adjustments
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.CONFIG_ADJUSTMENT_REASONS)]
    public class AdjustmentReasonsController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdjustmentReasonsController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: AdjustmentReasons
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below.
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<AdjustmentReason>());
        }

        // GET: AdjustmentReasons/DataTable
        [HttpGet]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 0;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var collection = _unitOfWork.Query<AdjustmentReason>();

            var recordsTotal = collection.Count();

            var query = collection;
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                query = query.Where(r =>
                    (r.Name != null && r.Name.ToLower().Contains(s)) ||
                    (r.Description != null && r.Description.ToLower().Contains(s)));
            }

            var recordsFiltered = query.Count();

            // Column index → AdjustmentReason field. Matches the `columns` array in Index.cshtml.
            // 0 = Name, 1 = Description, 2 = IsActive, 3 = IsInbound, 4 = Actions (not sortable).
            bool desc = sortDir == "desc";
            query = (sortColumnIndex, desc) switch
            {
                (1, true) => query.OrderByDescending(r => r.Description),
                (1, false) => query.OrderBy(r => r.Description),
                (2, true) => query.OrderByDescending(r => r.IsActive),
                (2, false) => query.OrderBy(r => r.IsActive),
                (3, true) => query.OrderByDescending(r => r.IsInbound),
                (3, false) => query.OrderBy(r => r.IsInbound),
                (_, true) => query.OrderByDescending(r => r.Name),
                (_, false) => query.OrderBy(r => r.Name)
            };

            var pageItems = query.Skip(start).Take(length).ToList();
            var data = pageItems.Select(r => new
            {
                id = r.Id,
                name = r.Name ?? string.Empty,
                description = r.Description ?? string.Empty,
                isActive = r.IsActive,
                isInbound = r.IsInbound,
                cssClass = r.CssClass ?? string.Empty
            }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        public async Task<IActionResult> Details(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var adjustmentReason = await _unitOfWork.Repository<AdjustmentReason>().GetByIdAsync(id.Value, ct);
            if (adjustmentReason == null) return NotFound();
            return View(adjustmentReason);
        }

        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdjustmentReason adjustmentReason, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                adjustmentReason.IsActive = true;
                await _unitOfWork.Repository<AdjustmentReason>().AddAsync(adjustmentReason, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(adjustmentReason);
        }

        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var adjustmentReason = await _unitOfWork.Repository<AdjustmentReason>().GetByIdAsync(id.Value, ct);
            if (adjustmentReason == null) return NotFound();
            return View(adjustmentReason);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdjustmentReason adjustmentReason, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id != adjustmentReason.Id) return NotFound();
            if (ModelState.IsValid)
            {
                // The bound `adjustmentReason` has no TenantId from the form — UpdateAsync marks
                // every property Modified, so saving as-is would zero the real TenantId, orphaning
                // the row from every tenant's query filter.
                var existingTenantId = await _unitOfWork.Query<AdjustmentReason>()
                    .Where(r => r.Id == adjustmentReason.Id).Select(r => r.TenantId).FirstOrDefaultAsync(ct);
                if (existingTenantId == 0) return NotFound();
                adjustmentReason.TenantId = existingTenantId;

                await _unitOfWork.Repository<AdjustmentReason>().UpdateAsync(adjustmentReason, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(adjustmentReason);
        }

        public async Task<IActionResult> Delete(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var adjustmentReason = await _unitOfWork.Repository<AdjustmentReason>().GetByIdAsync(id.Value, ct);
            if (adjustmentReason == null) return NotFound();
            return View(adjustmentReason);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (await AdjustmentReasonExistsAsync(id, ct))
            {
                await _unitOfWork.Repository<AdjustmentReason>().DeleteAsync(id, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> AdjustmentReasonExistsAsync(int id, CancellationToken ct = default)
        {
            return await _unitOfWork.Repository<AdjustmentReason>().ExistsAsync(id, ct);
        }
    }
}
