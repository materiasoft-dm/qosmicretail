using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Net.Codecrete.QrCodeGenerator;

namespace Mercurius.Controllers.Api
{
    // Products for the Blazor WASM client — full parity with the MVC ProductsController: barcode,
    // per-category custom fields, photo upload, QR code, and CSV import. Cookie-authenticated (the
    // WASM app is same-origin), tenant-isolated by MercuriusDbContext's global query filter.
    [ApiController]
    [Route("api/products")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PRODUCTS_INDEX)]
    public class ProductsApiController : ControllerBase
    {
        private static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg" };
        private const long MaxImageBytes = 5 * 1024 * 1024;
        private const long MaxImportBytes = 20 * 1024 * 1024;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly Mercurius.Services.ImportJobTracker _importJobTracker;
        private readonly Mercurius.Services.ProductImportQueue _importQueue;

        public ProductsApiController(IUnitOfWork unitOfWork, IWebHostEnvironment env,
            Mercurius.Services.ImportJobTracker importJobTracker, Mercurius.Services.ProductImportQueue importQueue)
        {
            _unitOfWork = unitOfWork;
            _env = env;
            _importJobTracker = importJobTracker;
            _importQueue = importQueue;
        }

        // ------------------------------------------------------------------ list

        [HttpGet]
        public ActionResult<ProductListResultDto> Get(
            string? search = null,
            int? categoryId = null,
            int page = 1,
            int pageSize = 25,
            string? sort = null,
            bool desc = false,
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
                    (p.ProductCode != null && p.ProductCode.ToLower().Contains(s)) ||
                    (p.Barcode != null && p.Barcode.ToLower().Contains(s)));
            }
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.ProductCategoryId == categoryId.Value);
            }

            var total = query.Count();
            // Decimal sort keys are cast to double: SQLite can't ORDER BY a decimal column at
            // all (NotSupportedException), and the precision lost is irrelevant for ordering.
            IOrderedQueryable<Product> ordered = (sort ?? string.Empty).ToLowerInvariant() switch
            {
                "code" => desc ? query.OrderByDescending(p => p.ProductCode) : query.OrderBy(p => p.ProductCode),
                "cost" => desc ? query.OrderByDescending(p => (double?)p.CurrentCostPrice) : query.OrderBy(p => (double?)p.CurrentCostPrice),
                "price" => desc ? query.OrderByDescending(p => (double?)p.CurrentSalePrice) : query.OrderBy(p => (double?)p.CurrentSalePrice),
                "stock" => desc ? query.OrderByDescending(p => (double)p.CurrentStock) : query.OrderBy(p => (double)p.CurrentStock),
                "status" => desc ? query.OrderByDescending(p => p.IsActive) : query.OrderBy(p => p.IsActive),
                _ => desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name)
            };
            var pageItems = ordered.ThenBy(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var categoryIds = pageItems.Where(p => p.ProductCategoryId.HasValue)
                .Select(p => p.ProductCategoryId!.Value).Distinct().ToList();
            var categoryLookup = categoryIds.Count == 0
                ? new Dictionary<int, string>()
                : _unitOfWork.Query<ProductCategory>()
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToDictionary(c => c.Id, c => c.Name);

            var items = pageItems.Select(p => new ProductListItemDto
            {
                Id = p.Id,
                ProductCode = p.ProductCode ?? string.Empty,
                Barcode = p.Barcode,
                Name = p.Name ?? string.Empty,
                Category = p.ProductCategoryId.HasValue && categoryLookup.TryGetValue(p.ProductCategoryId.Value, out var cn) ? cn : null,
                Stock = p.CurrentStock,
                CostPrice = p.CurrentCostPrice,
                SalePrice = p.CurrentSalePrice,
                LowStockCount = p.LowStockCount,
                IsActive = p.IsActive
            }).ToList();

            return Ok(new ProductListResultDto { Items = items, Total = total });
        }

        // ------------------------------------------------------------------ create / read / update

        [HttpPost]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PRODUCTS_CREATE)]
        public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ProductCode))
            {
                return BadRequest(new { error = "Name and SKU are required." });
            }

            var product = new Product
            {
                ProductCode = request.ProductCode.Trim(),
                Barcode = NormalizeBarcode(request.Barcode),
                Name = request.Name.Trim(),
                Description = request.Description ?? string.Empty,
                ProductCategoryId = request.ProductCategoryId,
                CurrentCostPrice = request.CurrentCostPrice,
                MarkUpPercentage = request.MarkUpPercentage,
                CurrentSalePrice = request.CurrentSalePrice,
                LeadTimeDays = request.LeadTimeDays,
                LowStockCount = request.LowStockCount,
                Note = request.Note ?? string.Empty,
                CustomWarning = string.Empty,
                Model = string.Empty,
                ImageFilename = string.Empty,
                IsActive = true,
                CreateDate = DateTime.UtcNow,
                CreatedBy = CurrentUserId
            };
            await _unitOfWork.Repository<Product>().AddAsync(product, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await SaveCustomFieldsAsync(product.Id, product.ProductCategoryId, request.CustomFieldValues, ct);
            return Ok(new { id = product.Id });
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProductDetailDto>> GetDetail(int id, CancellationToken ct = default)
        {
            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id, ct);
            if (product == null) return NotFound();

            var categoryName = product.ProductCategoryId.HasValue
                ? _unitOfWork.Query<ProductCategory>().Where(c => c.Id == product.ProductCategoryId.Value).Select(c => c.Name).FirstOrDefault()
                : null;

            return Ok(new ProductDetailDto
            {
                Id = product.Id,
                ProductCode = product.ProductCode ?? string.Empty,
                Barcode = product.Barcode,
                Name = product.Name ?? string.Empty,
                Description = product.Description,
                ProductCategoryId = product.ProductCategoryId,
                CategoryName = categoryName,
                CurrentCostPrice = product.CurrentCostPrice,
                MarkUpPercentage = product.MarkUpPercentage,
                CurrentSalePrice = product.CurrentSalePrice,
                LeadTimeDays = product.LeadTimeDays,
                CurrentStock = product.CurrentStock,
                LowStockCount = product.LowStockCount,
                Note = product.Note,
                Model = product.Model,
                CustomWarning = product.CustomWarning,
                IsActive = product.IsActive,
                ImageUrl = string.IsNullOrWhiteSpace(product.ImageFilename) ? null : $"/ProductImages/{product.ImageFilename}",
                CreateDate = product.CreateDate,
                UpdatedDate = product.UpdatedDate,
                CustomFields = product.ProductCategoryId.HasValue
                    ? LoadCustomFields(product.ProductCategoryId.Value, product.Id)
                    : new List<ProductCustomFieldDto>()
            });
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PRODUCTS_EDIT)]
        public async Task<IActionResult> Update(int id, UpdateProductRequest request, CancellationToken ct = default)
        {
            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id, ct);
            if (product == null) return NotFound();

            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ProductCode))
            {
                return BadRequest(new { error = "Name and SKU are required." });
            }

            product.ProductCode = request.ProductCode.Trim();
            product.Barcode = NormalizeBarcode(request.Barcode);
            product.Name = request.Name.Trim();
            product.Description = request.Description ?? string.Empty;
            product.ProductCategoryId = request.ProductCategoryId;
            product.CurrentCostPrice = request.CurrentCostPrice;
            product.MarkUpPercentage = request.MarkUpPercentage;
            product.CurrentSalePrice = request.CurrentSalePrice;
            product.LeadTimeDays = request.LeadTimeDays;
            product.LowStockCount = request.LowStockCount;
            product.Note = request.Note ?? string.Empty;
            // IsActive=false is this app's "delete" (soft) — the MVC Delete hard-deleted, which
            // fails once any invoice/batch references the product anyway (FKs are Restrict).
            product.IsActive = request.IsActive;
            product.UpdatedDate = DateTime.UtcNow;
            product.UpdatedBy = CurrentUserId;

            await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await SaveCustomFieldsAsync(product.Id, product.ProductCategoryId, request.CustomFieldValues, ct);
            return Ok();
        }

        [HttpGet("categories")]
        public ActionResult<List<Mercurius.Shared.Configuration.ProductCategoryDto>> GetCategoryOptions()
        {
            var items = _unitOfWork.Query<ProductCategory>().OrderBy(c => c.Name)
                .Select(c => new Mercurius.Shared.Configuration.ProductCategoryDto { Id = c.Id, Name = c.Name ?? string.Empty, IsActive = c.IsActive })
                .ToList();
            return Ok(items);
        }

        // The custom field definitions for a category (with a product's values when productId is
        // given) — the product form reloads these when the category changes.
        [HttpGet("category-fields/{categoryId:int}")]
        public ActionResult<List<ProductCustomFieldDto>> GetCategoryFields(int categoryId, int? productId = null)
            => Ok(LoadCustomFields(categoryId, productId));

        // ------------------------------------------------------------------ photo / QR

        [HttpPost("{id:int}/photo")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PRODUCTS_EDIT)]
        [RequestSizeLimit(MaxImageBytes + 64 * 1024)]
        public async Task<IActionResult> UploadPhoto(int id, IFormFile? file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0) return BadRequest(new { error = "Choose an image to upload." });
            if (file.Length > MaxImageBytes) return BadRequest(new { error = "The image must be 5 MB or smaller." });
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext)) return BadRequest(new { error = "Only PNG or JPG images are supported." });

            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id, ct);
            if (product == null) return NotFound();

            // Same storage as the MVC upload (wwwroot/ProductImages, random name) so both UIs and
            // existing photos keep working unchanged.
            var folder = Path.Combine(_env.WebRootPath, "ProductImages");
            Directory.CreateDirectory(folder);
            var storedName = $"{Guid.NewGuid():N}{ext}";
            await using (var stream = new FileStream(Path.Combine(folder, storedName), FileMode.Create))
            {
                await file.CopyToAsync(stream, ct);
            }

            product.ImageFilename = storedName;
            product.UpdatedDate = DateTime.UtcNow;
            product.UpdatedBy = CurrentUserId;
            await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new { imageUrl = $"/ProductImages/{storedName}" });
        }

        // Same payload as the MVC QRCode action (ProductCode + Name), as SVG.
        [HttpGet("{id:int}/qrcode")]
        public async Task<IActionResult> GetQrCode(int id, CancellationToken ct = default)
        {
            var product = await _unitOfWork.Repository<Product>().GetByIdAsync(id, ct);
            if (product == null) return NotFound();

            var qr = QrCode.EncodeText($"ProductCode:{product.ProductCode}|Name:{product.Name}", QrCode.Ecc.Medium);
            return File(System.Text.Encoding.UTF8.GetBytes(qr.ToSvgString(border: 4)), "image/svg+xml");
        }

        // ------------------------------------------------------------------ CSV import

        // Queues the same background import the MVC Import page uses (ProductImportBackgroundService)
        // and returns a job id to poll.
        [HttpPost("import")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PRODUCTS_CREATE)]
        [RequestSizeLimit(MaxImportBytes + 64 * 1024)]
        public async Task<ActionResult<ProductImportStartedDto>> StartImport(IFormFile? csvFile, [FromForm] bool updateExisting = false, CancellationToken ct = default)
        {
            if (csvFile == null || csvFile.Length == 0) return BadRequest(new { error = "Please select a CSV file to upload." });
            if (csvFile.Length > MaxImportBytes) return BadRequest(new { error = "The file must be 20 MB or smaller." });
            if (!Path.GetExtension(csvFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Please upload a CSV file." });
            }

            byte[] content;
            using (var ms = new MemoryStream())
            {
                await csvFile.CopyToAsync(ms, ct);
                content = ms.ToArray();
            }

            var jobId = Guid.NewGuid().ToString();
            _importJobTracker.CreateJob(jobId);
            await _importQueue.EnqueueAsync(new Mercurius.Services.ProductImportJob
            {
                JobId = jobId,
                FileContent = content,
                UpdateExisting = updateExisting,
                TenantId = int.TryParse(User.FindFirstValue(MercuriusClaimTypes.TenantId), out var tid) ? tid : 0
            }, ct);

            return Ok(new ProductImportStartedDto { JobId = jobId });
        }

        [HttpGet("import/{jobId}")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.PRODUCTS_CREATE)]
        public ActionResult<ProductImportStatusDto> GetImportStatus(string jobId)
        {
            var state = _importJobTracker.Get(jobId);
            if (state == null) return NotFound();
            return Ok(new ProductImportStatusDto
            {
                Status = state.Status,
                Total = state.Total,
                Processed = state.Processed,
                Created = state.Created,
                Updated = state.Updated,
                Skipped = state.Skipped,
                Errors = state.Errors,
                Log = _importJobTracker.SnapshotLog(state),
                ErrorMessage = state.ErrorMessage
            });
        }

        // ------------------------------------------------------------------ helpers

        private Guid CurrentUserId =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;

        private static string? NormalizeBarcode(string? barcode) =>
            string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();

        private List<ProductCustomFieldDto> LoadCustomFields(int categoryId, int? productId)
        {
            var fields = _unitOfWork.Query<CategoryField>()
                .Where(f => f.CategoryId == categoryId && f.IsActive)
                .OrderBy(f => f.SortOrder)
                .ToList();

            var values = productId.HasValue
                ? _unitOfWork.Query<ProductField>().Where(pf => pf.ProductId == productId.Value)
                    .ToList().GroupBy(pf => pf.CategoryFieldId).ToDictionary(g => g.Key, g => g.First().Value)
                : new Dictionary<int, string>();

            return fields.Select(f => new ProductCustomFieldDto
            {
                CategoryFieldId = f.Id,
                FieldName = f.FieldName ?? string.Empty,
                DisplayLabel = string.IsNullOrWhiteSpace(f.DisplayLabel) ? f.FieldName ?? string.Empty : f.DisplayLabel,
                FieldType = string.IsNullOrWhiteSpace(f.FieldType) ? "text" : f.FieldType,
                Options = (CategoryFieldsApiController.OptionsToDisplay(f.Options) ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                IsRequired = f.IsRequired,
                Value = values.TryGetValue(f.Id, out var v) ? v : null
            }).ToList();
        }

        // Mirrors ProductsController.SaveCustomFieldsAsync: replace this product's values with the
        // submitted ones, keeping only fields that belong to its current category (so values from
        // a previous category don't linger after a category change).
        private async Task SaveCustomFieldsAsync(int productId, int? categoryId, Dictionary<int, string?>? submitted, CancellationToken ct)
        {
            var repo = _unitOfWork.Repository<ProductField>();
            var existing = await repo.FindAsync(pf => pf.ProductId == productId, ct);
            foreach (var pf in existing)
            {
                await repo.DeleteAsync(pf.Id, ct);
            }

            if (categoryId.HasValue && submitted != null && submitted.Count > 0)
            {
                var validFieldIds = _unitOfWork.Query<CategoryField>()
                    .Where(f => f.CategoryId == categoryId.Value)
                    .Select(f => f.Id)
                    .ToHashSet();

                foreach (var (fieldId, value) in submitted)
                {
                    if (!validFieldIds.Contains(fieldId) || string.IsNullOrWhiteSpace(value)) continue;
                    await repo.AddAsync(new ProductField { ProductId = productId, CategoryFieldId = fieldId, Value = value.Trim() }, ct);
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }
    }
}
