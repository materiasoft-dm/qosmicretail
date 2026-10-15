using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Common;
using Mercurius.Shared.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    [ApiController]
    [Route("api/category-fields")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CONFIG_CATEGORY_FIELDS)]
    public class CategoryFieldsApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public CategoryFieldsApiController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        [HttpGet]
        public ActionResult<ListResultDto<CategoryFieldDto>> Get(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var fields = _unitOfWork.Query<CategoryField>().OrderBy(f => f.CategoryId).ThenBy(f => f.SortOrder).ToList();
            var categoryLookup = _unitOfWork.Query<ProductCategory>().ToDictionary(c => c.Id, c => c.Name ?? string.Empty);

            var items = fields.Select(f => new CategoryFieldDto
            {
                Id = f.Id,
                CategoryId = f.CategoryId,
                CategoryName = categoryLookup.TryGetValue(f.CategoryId, out var n) ? n : string.Empty,
                FieldName = f.FieldName,
                DisplayLabel = f.DisplayLabel,
                FieldType = f.FieldType,
                SortOrder = f.SortOrder,
                Options = OptionsToDisplay(f.Options),
                IsRequired = f.IsRequired,
                IsActive = f.IsActive
            }).ToList();
            return Ok(new ListResultDto<CategoryFieldDto> { Items = items, Total = items.Count });
        }

        // IsActive=false is the delete (soft) — a hard delete would orphan every product's saved
        // value for this field (ProductField rows).
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, UpdateCategoryFieldRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.FieldName) || request.CategoryId <= 0)
            {
                return BadRequest(new { error = "Category and field name are required." });
            }
            var field = await _unitOfWork.Repository<CategoryField>().GetByIdAsync(id, ct);
            if (field == null) return NotFound();

            var fieldName = request.FieldName.Trim();
            var duplicate = _unitOfWork.Query<CategoryField>()
                .Any(f => f.Id != id && f.CategoryId == request.CategoryId && f.FieldName == fieldName);
            if (duplicate)
            {
                return BadRequest(new { error = "A field with this name already exists in the selected category." });
            }

            field.CategoryId = request.CategoryId;
            field.FieldName = fieldName;
            field.DisplayLabel = string.IsNullOrWhiteSpace(request.DisplayLabel) ? fieldName : request.DisplayLabel.Trim();
            field.FieldType = request.FieldType;
            field.SortOrder = request.SortOrder;
            field.Options = OptionsFromDisplay(request.Options);
            field.IsRequired = request.IsRequired;
            field.IsActive = request.IsActive;
            await _unitOfWork.Repository<CategoryField>().UpdateAsync(field, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return NoContent();
        }

        // CategoryField.Options is stored as a JSON array (["Tablet","Capsule"]) — that's what the
        // product forms parse (ProductsController.GetCategoryFields, and the seed data). The Blazor
        // page edits it as friendlier comma-separated text; convert at the boundary so the stored
        // format never changes.
        internal static string? OptionsToDisplay(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            try
            {
                var values = System.Text.Json.JsonSerializer.Deserialize<string[]>(stored);
                return values == null ? null : string.Join(", ", values);
            }
            catch (System.Text.Json.JsonException)
            {
                return stored; // legacy/non-JSON value — show it as-is rather than lose it
            }
        }

        internal static string? OptionsFromDisplay(string? display)
        {
            if (string.IsNullOrWhiteSpace(display)) return null;
            var values = display.Split(',')
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .Distinct(System.StringComparer.Ordinal)
                .ToArray();
            return values.Length == 0 ? null : System.Text.Json.JsonSerializer.Serialize(values);
        }

        [HttpGet("categories")]
        public ActionResult<List<ProductCategoryDto>> GetCategories()
        {
            var items = _unitOfWork.Query<ProductCategory>().OrderBy(c => c.Name)
                .Select(c => new ProductCategoryDto { Id = c.Id, Name = c.Name ?? string.Empty, IsActive = c.IsActive })
                .ToList();
            return Ok(items);
        }

        [HttpPost]
        public async Task<ActionResult<CategoryFieldDto>> Create(CreateCategoryFieldRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.FieldName) || request.CategoryId <= 0)
            {
                return BadRequest(new { error = "Category and field name are required." });
            }

            var fieldName = request.FieldName.Trim();
            var duplicate = _unitOfWork.Query<CategoryField>()
                .Any(f => f.CategoryId == request.CategoryId && f.FieldName == fieldName);
            if (duplicate)
            {
                return BadRequest(new { error = "A field with this name already exists in the selected category." });
            }

            var field = new CategoryField
            {
                CategoryId = request.CategoryId,
                FieldName = fieldName,
                DisplayLabel = string.IsNullOrWhiteSpace(request.DisplayLabel) ? fieldName : request.DisplayLabel.Trim(),
                FieldType = string.IsNullOrWhiteSpace(request.FieldType) ? "text" : request.FieldType,
                SortOrder = request.SortOrder,
                Options = OptionsFromDisplay(request.Options),
                IsRequired = request.IsRequired,
                IsActive = true
            };
            await _unitOfWork.Repository<CategoryField>().AddAsync(field, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var categoryName = _unitOfWork.Query<ProductCategory>().Where(c => c.Id == field.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty;
            return Ok(new CategoryFieldDto
            {
                Id = field.Id,
                CategoryId = field.CategoryId,
                CategoryName = categoryName,
                FieldName = field.FieldName,
                DisplayLabel = field.DisplayLabel,
                FieldType = field.FieldType,
                SortOrder = field.SortOrder,
                Options = OptionsToDisplay(field.Options),
                IsRequired = field.IsRequired,
                IsActive = field.IsActive
            });
        }
    }
}
