using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers.Configurations
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.CONFIG_COLORS)]
    public class ColorsController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public ColorsController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: Colors
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below.
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<Color>());
        }

        // GET: Colors/DataTable
        // Server-side endpoint for jQuery DataTables.
        [HttpGet]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 0;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            // Only one sortable column on this table.
            string sortField = nameof(Color.Name);

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var collection = _unitOfWork.GetCollection<Color>();

            var recordsTotal = collection.Count();

            var query = collection.Query();
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                query = query.Where(c => c.Name != null && c.Name.ToLower().Contains(s));
            }

            var recordsFiltered = query.Count();

            var bsonField = LiteDB.BsonExpression.Create($"$.{sortField}");
            query = sortDir == "desc"
                ? query.OrderByDescending(bsonField)
                : query.OrderBy(bsonField);

            var pageItems = query.Skip(start).Limit(length).ToList();
            var data = pageItems.Select(c => new { id = c.Id, name = c.Name ?? string.Empty }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        public async Task<IActionResult> Details(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var color = await _unitOfWork.Repository<Color>().GetByIdAsync(id.Value, ct);
            if (color == null) return NotFound();
            return View(color);
        }

        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name")] Color color, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                await _unitOfWork.Repository<Color>().AddAsync(color, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(color);
        }

        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var color = await _unitOfWork.Repository<Color>().GetByIdAsync(id.Value, ct);
            if (color == null) return NotFound();
            return View(color);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Color color, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id != color.Id) return NotFound();
            if (ModelState.IsValid)
            {
                if (!await ColorExistsAsync(color.Id, ct)) return NotFound();
                await _unitOfWork.Repository<Color>().UpdateAsync(color, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(color);
        }

        public async Task<IActionResult> Delete(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var color = await _unitOfWork.Repository<Color>().GetByIdAsync(id.Value, ct);
            if (color == null) return NotFound();
            return View(color);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (await ColorExistsAsync(id, ct))
            {
                await _unitOfWork.Repository<Color>().DeleteAsync(id, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> ColorExistsAsync(int id, CancellationToken ct = default)
        {
            return await _unitOfWork.Repository<Color>().ExistsAsync(id, ct);
        }
    }
}
