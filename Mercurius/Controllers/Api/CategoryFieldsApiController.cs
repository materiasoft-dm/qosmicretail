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
                SortOrder = f.SortOrder
            }).ToList();
            return Ok(new ListResultDto<CategoryFieldDto> { Items = items, Total = items.Count });
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

            var duplicate = _unitOfWork.Query<CategoryField>()
                .Any(f => f.CategoryId == request.CategoryId && f.FieldName == request.FieldName);
            if (duplicate)
            {
                return BadRequest(new { error = "A field with this name already exists in the selected category." });
            }

            var nextSortOrder = _unitOfWork.Query<CategoryField>().Where(f => f.CategoryId == request.CategoryId)
                .Select(f => (int?)f.SortOrder).Max() ?? -1;

            var field = new CategoryField
            {
                CategoryId = request.CategoryId,
                FieldName = request.FieldName,
                DisplayLabel = request.DisplayLabel,
                FieldType = request.FieldType,
                SortOrder = nextSortOrder + 1
            };
            await _unitOfWork.Repository<CategoryField>().AddAsync(field, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var categoryName = _unitOfWork.Query<ProductCategory>().Where(c => c.Id == field.CategoryId).Select(c => c.Name).FirstOrDefault() ?? string.Empty;
            return Ok(new CategoryFieldDto { Id = field.Id, CategoryId = field.CategoryId, CategoryName = categoryName, FieldName = field.FieldName, DisplayLabel = field.DisplayLabel, FieldType = field.FieldType, SortOrder = field.SortOrder });
        }
    }
}
