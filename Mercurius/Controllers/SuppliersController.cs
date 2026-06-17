using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.CONFIG_SUPPLIERS)]
    public class SuppliersController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public SuppliersController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: Suppliers
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below. We pass an empty list rather than load
        // every Supplier on every page hit.
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<Supplier>());
        }

        // GET: Suppliers/DataTable
        // Server-side endpoint for jQuery DataTables. Honors the standard request
        // shape (draw / start / length / order[0][column] / order[0][dir] / search[value])
        // and replies with { draw, recordsTotal, recordsFiltered, data: [...] }.
        // All filtering/ordering/paging is pushed down to LiteDB so the browser
        // only ever receives one page of rows.
        [HttpGet]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            // ASP.NET model-binds DataTables' bracketed keys straight from the query string.
            // Grab them off the raw Request.Query because the built-in binder would need a
            // flat DTO that mirrors all of DataTables' shapes.
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 0;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            // Column index → Supplier field. Matches the `columns` array in Index.cshtml.
            // 0 = Name, 1 = IsActive, 2 = Actions (not sortable).
            string sortField = sortColumnIndex switch
            {
                0 => nameof(Supplier.Name),
                1 => nameof(Supplier.IsActive),
                _ => nameof(Supplier.Name)
            };
            if (length < 1) length = 25;
            if (length > 200) length = 200; // hard cap so a malicious client can't ask for the world

            var collection = _unitOfWork.GetCollection<Supplier>();

            // Match the legacy Index behavior: only active (not soft-deleted) suppliers.
            // recordsTotal counts the active set, not the entire collection, so the
            // "Showing X of Y" footer reflects what the user is actually paging through.
            var baseQuery = collection.Query().Where(sp => sp.IsActive);
            var recordsTotal = baseQuery.Count();

            var query = baseQuery;
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                query = query.Where(sp => sp.Name != null && sp.Name.ToLower().Contains(s));
            }

            var recordsFiltered = query.Count();

            var bsonField = LiteDB.BsonExpression.Create($"$.{sortField}");
            query = sortDir == "desc"
                ? query.OrderByDescending(bsonField)
                : query.OrderBy(bsonField);

            var pageItems = query.Skip(start).Limit(length).ToList();

            // Project to plain JSON. Visual decoration (status badge, action buttons) is
            // handled client-side in DataTables' columns.render callbacks — see
            // Views/Suppliers/Index.cshtml. Keeping the payload data-only means we don't
            // have to HTML-encode strings here.
            var data = pageItems.Select(sp => new
            {
                id = sp.Id,
                name = sp.Name ?? string.Empty,
                isActive = sp.IsActive
            }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        public async Task<IActionResult> Details(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _unitOfWork.Repository<Supplier>().GetByIdAsync(id.Value, ct);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,IsActive")] Supplier supplier, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                supplier.IsActive = true;
                await _unitOfWork.Repository<Supplier>().AddAsync(supplier, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _unitOfWork.Repository<Supplier>().GetByIdAsync(id.Value, ct);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,IsActive")] Supplier supplier, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id != supplier.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                if (!await SupplierExistsAsync(supplier.Id, ct))
                {
                    return NotFound();
                }

                await _unitOfWork.Repository<Supplier>().UpdateAsync(supplier, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        public async Task<IActionResult> Delete(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _unitOfWork.Repository<Supplier>().GetByIdAsync(id.Value, ct);
            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var supplier = await _unitOfWork.Repository<Supplier>().GetByIdAsync(id, ct);
            if (supplier != null)
            {
                supplier.IsActive = false;
                await _unitOfWork.Repository<Supplier>().UpdateAsync(supplier, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> SupplierExistsAsync(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return await _unitOfWork.Repository<Supplier>().ExistsAsync(id, ct);
        }
    }
}
