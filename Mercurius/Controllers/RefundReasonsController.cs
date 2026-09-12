using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    // CRUD shape mirrors Controllers/Adjustments/AdjustmentReasonsController.cs, with one
    // deliberate difference: no hard-delete action. "Archived" here means IsActive = false,
    // toggled via the Edit form's checkbox — matches CLAUDE.md's soft-delete convention, which
    // AdjustmentReasonsController itself predates and doesn't follow.
    [Authorize(Policy = Common.ModuleRegistry.Pages.CONFIG_REFUND_REASONS)]
    public class RefundReasonsController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public RefundReasonsController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: RefundReasons
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<RefundReason>());
        }

        // GET: RefundReasons/DataTable
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

            var collection = _unitOfWork.Query<RefundReason>();

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

            // Column index → RefundReason field. Matches the `columns` array in Index.cshtml.
            // 0 = Name, 1 = Description, 2 = IsActive, 3 = Actions (not sortable).
            bool desc = sortDir == "desc";
            query = (sortColumnIndex, desc) switch
            {
                (1, true) => query.OrderByDescending(r => r.Description),
                (1, false) => query.OrderBy(r => r.Description),
                (2, true) => query.OrderByDescending(r => r.IsActive),
                (2, false) => query.OrderBy(r => r.IsActive),
                (_, true) => query.OrderByDescending(r => r.Name),
                (_, false) => query.OrderBy(r => r.Name)
            };

            var pageItems = query.Skip(start).Take(length).ToList();
            var data = pageItems.Select(r => new
            {
                id = r.Id,
                name = r.Name ?? string.Empty,
                description = r.Description ?? string.Empty,
                isActive = r.IsActive
            }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            // Pass an actual instance rather than a null model — otherwise the Create view's
            // hidden `asp-for="Id"` input can't resolve a default value and renders value="",
            // which fails model binding (empty string isn't a valid int) and silently breaks
            // every submission of this form. AdjustmentReasonsController.Create has this same
            // bug; not fixing it there too since it's out of scope here.
            return View(new RefundReason());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RefundReason refundReason, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                refundReason.IsActive = true;
                await _unitOfWork.Repository<RefundReason>().AddAsync(refundReason, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(refundReason);
        }

        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var refundReason = await _unitOfWork.Repository<RefundReason>().GetByIdAsync(id.Value, ct);
            if (refundReason == null) return NotFound();
            return View(refundReason);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RefundReason refundReason, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id != refundReason.Id) return NotFound();
            if (ModelState.IsValid)
            {
                // Not using Repository<T>().ExistsAsync(id) here — it fetches (and so tracks) the
                // entity first, and then UpdateAsync's Attach(refundReason) throws
                // "already tracked" for the same key. Query<T>().Select(...) projects to a scalar,
                // so nothing gets tracked and the later Attach has a clean slate — this also
                // recovers the real TenantId: the bound `refundReason` above has none from the
                // form, and UpdateAsync marks every property Modified, so saving it as-is would
                // zero the real TenantId and orphan the row from every tenant's query filter.
                var existingTenantId = await _unitOfWork.Query<RefundReason>()
                    .Where(r => r.Id == id).Select(r => r.TenantId).FirstOrDefaultAsync(ct);
                if (existingTenantId == 0) return NotFound();
                refundReason.TenantId = existingTenantId;

                await _unitOfWork.Repository<RefundReason>().UpdateAsync(refundReason, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(refundReason);
        }
    }
}
