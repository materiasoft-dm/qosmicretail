using System;
using System.Linq;
using System.Security.Claims;
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
    [Route("api/product-categories")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CONFIG_PRODUCT_CATEGORIES)]
    public class ProductCategoriesApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public ProductCategoriesApiController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        [HttpGet]
        public ActionResult<ListResultDto<ProductCategoryDto>> Get(string? search = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var query = _unitOfWork.Query<ProductCategory>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(c => (c.Name != null && c.Name.ToLower().Contains(s)) || (c.Description != null && c.Description.ToLower().Contains(s)));
            }
            var items = query.OrderBy(c => c.Name).Select(c => new ProductCategoryDto
            {
                Id = c.Id,
                Name = c.Name ?? string.Empty,
                Description = c.Description,
                IsActive = c.IsActive
            }).ToList();
            return Ok(new ListResultDto<ProductCategoryDto> { Items = items, Total = items.Count });
        }

        [HttpPost]
        public async Task<ActionResult<ProductCategoryDto>> Create(CreateProductCategoryRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required." });
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var category = new ProductCategory
            {
                Name = request.Name,
                Description = request.Description,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty
            };
            await _unitOfWork.Repository<ProductCategory>().AddAsync(category, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(new ProductCategoryDto { Id = category.Id, Name = category.Name, Description = category.Description, IsActive = category.IsActive });
        }

        // IsActive=false is the delete (soft) — the MVC Delete hard-deleted, which also fails
        // outright once any product references the category (FKs are Restrict).
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, UpdateProductCategoryRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required." });
            var category = await _unitOfWork.Repository<ProductCategory>().GetByIdAsync(id, ct);
            if (category == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            category.Name = request.Name.Trim();
            category.Description = request.Description ?? string.Empty;
            category.IsActive = request.IsActive;
            category.UpdatedDate = DateTime.UtcNow;
            category.UpdatedBy = Guid.TryParse(userId, out var uid) ? uid : null;
            await _unitOfWork.Repository<ProductCategory>().UpdateAsync(category, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
