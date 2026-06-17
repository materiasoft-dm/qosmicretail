using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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

            // Column index → AdjustmentReason field. Matches the `columns` array in Index.cshtml.
            // 0 = Name, 1 = Description, 2 = IsActive, 3 = IsInbound, 4 = Actions (not sortable).
            string sortField = sortColumnIndex switch
            {
                0 => nameof(AdjustmentReason.Name),
                1 => nameof(AdjustmentReason.Description),
                2 => nameof(AdjustmentReason.IsActive),
                3 => nameof(AdjustmentReason.IsInbound),
                _ => nameof(AdjustmentReason.Name)
            };

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var collection = _unitOfWork.GetCollection<AdjustmentReason>();

            var recordsTotal = collection.Count();

            var query = collection.Query();
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                query = query.Where(r =>
                    (r.Name != null && r.Name.ToLower().Contains(s)) ||
                    (r.Description != null && r.Description.ToLower().Contains(s)));
            }

            var recordsFiltered = query.Count();

            var bsonField = LiteDB.BsonExpression.Create($"$.{sortField}");
            query = sortDir == "desc"
                ? query.OrderByDescending(bsonField)
                : query.OrderBy(bsonField);

            var pageItems = query.Skip(start).Limit(length).ToList();
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
                if (!await AdjustmentReasonExistsAsync(adjustmentReason.Id, ct)) return NotFound();
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
