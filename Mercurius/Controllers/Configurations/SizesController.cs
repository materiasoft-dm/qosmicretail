using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers.Configurations
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.CONFIG_SIZES)]
    public class SizesController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public SizesController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: Sizes
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below.
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<Size>());
        }

        // GET: Sizes/DataTable
        // Server-side endpoint for jQuery DataTables.
        [HttpGet]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            string sortField = nameof(Size.Name);

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var collection = _unitOfWork.GetCollection<Size>();

            var recordsTotal = collection.Count();

            var query = collection.Query();
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                query = query.Where(sz => sz.Name != null && sz.Name.ToLower().Contains(s));
            }

            var recordsFiltered = query.Count();

            var bsonField = LiteDB.BsonExpression.Create($"$.{sortField}");
            query = sortDir == "desc"
                ? query.OrderByDescending(bsonField)
                : query.OrderBy(bsonField);

            var pageItems = query.Skip(start).Limit(length).ToList();
            var data = pageItems.Select(sz => new { id = sz.Id, name = sz.Name ?? string.Empty }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        public async Task<IActionResult> Details(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var size = await _unitOfWork.Repository<Size>().GetByIdAsync(id.Value, ct);
            if (size == null) return NotFound();
            return View(size);
        }

        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name")] Size size, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                await _unitOfWork.Repository<Size>().AddAsync(size, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(size);
        }

        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var size = await _unitOfWork.Repository<Size>().GetByIdAsync(id.Value, ct);
            if (size == null) return NotFound();
            return View(size);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Size size, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id != size.Id) return NotFound();
            if (ModelState.IsValid)
            {
                if (!await SizeExistsAsync(size.Id, ct)) return NotFound();
                await _unitOfWork.Repository<Size>().UpdateAsync(size, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(size);
        }

        public async Task<IActionResult> Delete(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var size = await _unitOfWork.Repository<Size>().GetByIdAsync(id.Value, ct);
            if (size == null) return NotFound();
            return View(size);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (await SizeExistsAsync(id, ct))
            {
                await _unitOfWork.Repository<Size>().DeleteAsync(id, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> SizeExistsAsync(int id, CancellationToken ct = default)
        {
            return await _unitOfWork.Repository<Size>().ExistsAsync(id, ct);
        }
    }
}
