using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers.Configurations
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.CONFIG_PRODUCT_CATEGORIES)]
    public class ProductCategoriesController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProductCategoriesController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: ProductCategories
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below.
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<ProductCategory>());
        }

        // GET: ProductCategories/DataTable
        // Server-side endpoint. NOTE: the old view rendered CreatedBy / UpdatedBy as raw GUIDs,
        // which was unreadable. Those columns are omitted here — see the view for the new layout.
        [HttpGet]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 0;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            // Column index → ProductCategory field. Matches the `columns` array in Index.cshtml.
            // 0 = Name, 1 = Description, 2 = CreatedDate, 3 = IsActive, 4 = Actions.
            string sortField = sortColumnIndex switch
            {
                0 => nameof(ProductCategory.Name),
                1 => nameof(ProductCategory.Description),
                2 => nameof(ProductCategory.CreatedDate),
                3 => nameof(ProductCategory.IsActive),
                _ => nameof(ProductCategory.Name)
            };

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var collection = _unitOfWork.GetCollection<ProductCategory>();

            var recordsTotal = collection.Count();

            var query = collection.Query();
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                query = query.Where(c =>
                    (c.Name != null && c.Name.ToLower().Contains(s)) ||
                    (c.Description != null && c.Description.ToLower().Contains(s)));
            }

            var recordsFiltered = query.Count();

            var bsonField = LiteDB.BsonExpression.Create($"$.{sortField}");
            query = sortDir == "desc"
                ? query.OrderByDescending(bsonField)
                : query.OrderBy(bsonField);

            var pageItems = query.Skip(start).Limit(length).ToList();
            var data = pageItems.Select(c => new
            {
                id = c.Id,
                name = c.Name ?? string.Empty,
                description = c.Description ?? string.Empty,
                createdDate = c.CreatedDate,
                isActive = c.IsActive
            }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        public async Task<IActionResult> Details(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var productCategory = await _unitOfWork.Repository<ProductCategory>().GetByIdAsync(id.Value, ct);
            if (productCategory == null) return NotFound();
            return View(productCategory);
        }

        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,CreatedDate,CreatedBy,UpdatedDate,UpdatedBy,IsActive")] ProductCategory productCategory, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                productCategory.CreatedBy = GetLoggedInUserId();
                productCategory.CreatedDate = DateTime.UtcNow;
                productCategory.IsActive = true;

                await _unitOfWork.Repository<ProductCategory>().AddAsync(productCategory, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(productCategory);
        }

        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var productCategory = await _unitOfWork.Repository<ProductCategory>().GetByIdAsync(id.Value, ct);
            if (productCategory == null) return NotFound();
            return View(productCategory);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,CreatedDate,CreatedBy,UpdatedDate,UpdatedBy,IsActive")] ProductCategory productCategory, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id != productCategory.Id) return NotFound();
            if (ModelState.IsValid)
            {
                if (!await ProductCategoryExistsAsync(productCategory.Id, ct)) return NotFound();

                productCategory.UpdatedBy = GetLoggedInUserId();
                productCategory.UpdatedDate = DateTime.UtcNow;

                await _unitOfWork.Repository<ProductCategory>().UpdateAsync(productCategory, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(productCategory);
        }

        public async Task<IActionResult> Delete(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var productCategory = await _unitOfWork.Repository<ProductCategory>().GetByIdAsync(id.Value, ct);
            if (productCategory == null) return NotFound();
            return View(productCategory);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (await ProductCategoryExistsAsync(id, ct))
            {
                await _unitOfWork.Repository<ProductCategory>().DeleteAsync(id, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> ProductCategoryExistsAsync(int id, CancellationToken ct = default)
        {
            return await _unitOfWork.Repository<ProductCategory>().ExistsAsync(id, ct);
        }

        private Guid GetLoggedInUserId()
        {
            return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId
                : Guid.Empty;
        }
    }
}
