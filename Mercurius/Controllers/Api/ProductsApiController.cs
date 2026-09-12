using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // JSON equivalent of ProductsController.DataTable, for the Blazor WASM client — see
    // MULTITENANCY_ARCHITECTURE.md's Blazor conversion notes. Cookie-authenticated: the WASM app
    // is hosted from this same origin/project, so the browser sends the existing Identity cookie
    // automatically, same as any MVC page request. Tenant isolation needs no code here at all —
    // it comes from MercuriusDbContext's global query filter, exactly like every other consumer
    // of IUnitOfWork.Query<T>().
    [ApiController]
    [Route("api/products")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PRODUCTS_INDEX)]
    public class ProductsApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProductsApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public ActionResult<ProductListResultDto> Get(
            string? search = null,
            int? categoryId = null,
            int page = 1,
            int pageSize = 25,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200;

            var query = _unitOfWork.Query<Product>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(s)) ||
                    (p.ProductCode != null && p.ProductCode.ToLower().Contains(s)));
            }
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.ProductCategoryId == categoryId.Value);
            }

            var total = query.Count();
            var pageItems = query.OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var categoryIds = pageItems.Where(p => p.ProductCategoryId.HasValue)
                .Select(p => p.ProductCategoryId!.Value).Distinct().ToList();
            var categoryLookup = categoryIds.Count == 0
                ? new System.Collections.Generic.Dictionary<int, string>()
                : _unitOfWork.Query<ProductCategory>()
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToDictionary(c => c.Id, c => c.Name);

            var items = pageItems.Select(p => new ProductListItemDto
            {
                Id = p.Id,
                ProductCode = p.ProductCode ?? string.Empty,
                Name = p.Name ?? string.Empty,
                Category = p.ProductCategoryId.HasValue && categoryLookup.TryGetValue(p.ProductCategoryId.Value, out var cn) ? cn : null,
                Stock = p.CurrentStock,
                SalePrice = p.CurrentSalePrice,
                LowStockCount = p.LowStockCount,
                IsActive = p.IsActive
            }).ToList();

            return Ok(new ProductListResultDto { Items = items, Total = total });
        }

        // GET/PUT for the Edit page. Deliberately a smaller field set than
        // ProductsController.Edit (no image upload, no per-category custom fields) — the highest
        // -value core fields, not full parity; see MULTITENANCY_ARCHITECTURE.md's Blazor
        // conversion notes for the scope call.
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDetailDto>> GetDetail(int id, CancellationToken ct = default)
        {
            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id, ct);
            if (product == null) return NotFound();

            return Ok(new ProductDetailDto
            {
                Id = product.Id,
                ProductCode = product.ProductCode ?? string.Empty,
                Name = product.Name ?? string.Empty,
                Description = product.Description,
                ProductCategoryId = product.ProductCategoryId,
                CurrentCostPrice = product.CurrentCostPrice,
                MarkUpPercentage = product.MarkUpPercentage,
                CurrentSalePrice = product.CurrentSalePrice,
                LeadTimeDays = product.LeadTimeDays,
                CurrentStock = product.CurrentStock,
                LowStockCount = product.LowStockCount,
                Note = product.Note,
                IsActive = product.IsActive
            });
        }

        [HttpPut("{id}")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PRODUCTS_EDIT)]
        public async Task<IActionResult> Update(int id, UpdateProductRequest request, CancellationToken ct = default)
        {
            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id, ct);
            if (product == null) return NotFound();

            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ProductCode))
            {
                return BadRequest(new { error = "Name and SKU are required." });
            }

            product.ProductCode = request.ProductCode;
            product.Name = request.Name;
            product.Description = request.Description ?? string.Empty;
            product.ProductCategoryId = request.ProductCategoryId;
            product.CurrentCostPrice = request.CurrentCostPrice;
            product.MarkUpPercentage = request.MarkUpPercentage;
            product.CurrentSalePrice = request.CurrentSalePrice;
            product.LeadTimeDays = request.LeadTimeDays;
            product.LowStockCount = request.LowStockCount;
            product.Note = request.Note ?? string.Empty;
            product.IsActive = request.IsActive;
            product.UpdatedDate = DateTime.UtcNow;

            await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok();
        }

        [HttpGet("categories")]
        public ActionResult<System.Collections.Generic.List<Mercurius.Shared.Configuration.ProductCategoryDto>> GetCategoryOptions()
        {
            var items = _unitOfWork.Query<ProductCategory>().OrderBy(c => c.Name)
                .Select(c => new Mercurius.Shared.Configuration.ProductCategoryDto { Id = c.Id, Name = c.Name ?? string.Empty, IsActive = c.IsActive })
                .ToList();
            return Ok(items);
        }
    }
}
