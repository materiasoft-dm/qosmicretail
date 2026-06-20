using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mercurius.Controllers.Configurations
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.CONFIG_CATEGORY_FIELDS)]
    public class CategoryFieldsController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public CategoryFieldsController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var fields = await _unitOfWork.Repository<CategoryField>().GetAllAsync(ct);
            var categories = await _unitOfWork.Repository<ProductCategory>().GetAllAsync(ct);
            ViewBag.Categories = categories.ToDictionary(c => c.Id, c => c.Name);
            return View(fields.OrderBy(f => f.CategoryId).ThenBy(f => f.SortOrder));
        }

        public async Task<IActionResult> Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await LoadCategoriesViewBag(ct);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryField field, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                var existing = await _unitOfWork.Repository<CategoryField>()
                    .FindAsync(f => f.CategoryId == field.CategoryId && f.FieldName == field.FieldName, ct);
                if (existing.Any(f => f.Id != field.Id))
                {
                    ModelState.AddModelError("FieldName", "A field with this name already exists in the selected category.");
                    await LoadCategoriesViewBag(ct);
                    return View(field);
                }

                await _unitOfWork.Repository<CategoryField>().AddAsync(field, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(field);
        }

        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null) return NotFound();
            var field = await _unitOfWork.Repository<CategoryField>().GetByIdAsync(id.Value, ct);
            if (field == null) return NotFound();
            await LoadCategoriesViewBag(ct);
            return View(field);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryField field, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id != field.Id) return NotFound();
            if (ModelState.IsValid)
            {
                var existing = await _unitOfWork.Repository<CategoryField>()
                    .FindAsync(f => f.CategoryId == field.CategoryId && f.FieldName == field.FieldName, ct);
                if (existing.Any(f => f.Id != field.Id))
                {
                    ModelState.AddModelError("FieldName", "A field with this name already exists in the selected category.");
                    await LoadCategoriesViewBag(ct);
                    return View(field);
                }

                await _unitOfWork.Repository<CategoryField>().UpdateAsync(field, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(field);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await _unitOfWork.Repository<CategoryField>().DeleteAsync(id, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadCategoriesViewBag(CancellationToken ct = default)
        {
            var categories = await _unitOfWork.Repository<ProductCategory>().GetAllAsync(ct);
            ViewBag.CategoryId = new SelectList(categories, "Id", "Name");
        }
    }
}
