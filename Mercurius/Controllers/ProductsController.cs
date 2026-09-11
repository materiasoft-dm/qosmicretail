using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Mercurius.Repo.Models;
using Microsoft.AspNetCore.Authorization;
using Net.Codecrete.QrCodeGenerator;
using System.Text;
using Mercurius.Common.Helpers;
using Mercurius.Common.Constants;
using Mercurius.Helpers;
using Mercurius.Models;
using Mercurius.Repo;
using Mercurius.Repo.Repositories;
using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Hosting;

namespace Mercurius.Controllers
{
    [Authorize]
    public class ProductsController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly Mercurius.Services.ILoggerService _loggerService;
        private readonly Mercurius.Services.ImportJobTracker _importJobTracker;
        private readonly Mercurius.Services.ProductImportQueue _importQueue;

        public ProductsController(
            IHttpContextAccessor httpContextAccessor,
            IUnitOfWork unitOfWork,
            IWebHostEnvironment webHostEnvironment,
            Mercurius.Services.ILoggerService loggerService,
            Mercurius.Services.ImportJobTracker importJobTracker,
            Mercurius.Services.ProductImportQueue importQueue)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
            _loggerService = loggerService;
            _importJobTracker = importJobTracker;
            _importQueue = importQueue;
        }

        // GET: Products
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below (server-side processing). We pass an empty
        // list rather than load every Product on every page hit.
        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            await LoadViewBags(ct);
            return View(Enumerable.Empty<Product>());
        }

        // GET: Products/DataTable
        // Server-side endpoint for jQuery DataTables. Honors the standard request
        // shape (draw / start / length / order[0][column] / order[0][dir] / search[value])
        // and replies with { draw, recordsTotal, recordsFiltered, data: [...] }.
        // All filtering/ordering/paging is pushed down to the database so the browser
        // only ever receives one page of rows.
        [HttpGet]
        public IActionResult DataTable(
            int draw = 1,
            int start = 0,
            int length = 25,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            // ASP.NET model-binds DataTables' bracketed keys (order[0][column], search[value])
            // straight from the query string. Grab them off the raw Request.Query because the
            // built-in binder would need a flat DTO that mirrors all of DataTables' shapes.
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 1;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            if (length < 1) length = 25;
            if (length > 200) length = 200; // hard cap so a malicious client can't ask for the world

            // The repository abstraction's GetPagedAsync would force LINQ-to-objects ordering
            // for a caller-provided orderBy, so query the DbSet directly for native indexed paging.
            var collection = _unitOfWork.Query<Product>();

            // recordsTotal = total in the table, unfiltered.
            var recordsTotal = collection.Count();

            // Build the filtered query.
            var query = collection;
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                query = query.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(s)) ||
                    (p.ProductCode != null && p.ProductCode.ToLower().Contains(s)) ||
                    (p.Description != null && p.Description.ToLower().Contains(s)));
            }

            var recordsFiltered = query.Count();

            // Column index → Product field. Matches the `columns` array in Index.cshtml.
            // 0 = ProductCode, 1 = Name, 2 = Category, 3 = CurrentCostPrice,
            // 4 = CurrentSalePrice, 5 = CurrentStock, 6 = IsActive, 7 = Actions (not sortable).
            bool desc = sortDir == "desc";
            query = (sortColumnIndex, desc) switch
            {
                (0, true) => query.OrderByDescending(p => p.ProductCode),
                (0, false) => query.OrderBy(p => p.ProductCode),
                (3, true) => query.OrderByDescending(p => p.CurrentCostPrice),
                (3, false) => query.OrderBy(p => p.CurrentCostPrice),
                (4, true) => query.OrderByDescending(p => p.CurrentSalePrice),
                (4, false) => query.OrderBy(p => p.CurrentSalePrice),
                (5, true) => query.OrderByDescending(p => p.CurrentStock),
                (5, false) => query.OrderBy(p => p.CurrentStock),
                (6, true) => query.OrderByDescending(p => p.IsActive),
                (6, false) => query.OrderBy(p => p.IsActive),
                // Category isn't a sortable column on the entity.
                (_, true) => query.OrderByDescending(p => p.Name),
                (_, false) => query.OrderBy(p => p.Name)
            };

            var pageItems = query.Skip(start).Take(length).ToList();

            // Resolve category names in one shot (avoids N+1).
            var categoryIds = pageItems
                .Where(p => p.ProductCategoryId.HasValue)
                .Select(p => p.ProductCategoryId!.Value)
                .Distinct()
                .ToList();
            var categoryLookup = categoryIds.Count == 0
                ? new Dictionary<int, string>()
                : _unitOfWork.Query<ProductCategory>()
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToDictionary(c => c.Id, c => c.Name);

            // Project each row into plain JSON. All visual decoration (avatar block,
            // stock/status badges, action buttons) happens client-side in DataTables'
            // columns.render callbacks — see Views/Products/Index.cshtml. Keeping the
            // server payload data-only means we don't have to HTML-encode strings here
            // or rebuild the markup when the UI changes.
            var data = pageItems.Select(p => new
            {
                id = p.Id,
                productCode = p.ProductCode ?? string.Empty,
                name = p.Name ?? string.Empty,
                category = p.ProductCategoryId.HasValue
                    && categoryLookup.TryGetValue(p.ProductCategoryId.Value, out var cn) ? cn : string.Empty,
                costPrice = p.CurrentCostPrice,
                salePrice = p.CurrentSalePrice,
                stock = p.CurrentStock,
                lowStockCount = p.LowStockCount,
                isActive = p.IsActive
            }).ToList();

            return Json(new
            {
                draw,
                recordsTotal,
                recordsFiltered,
                data
            });
        }

        // GET: Products/GetForSaleModal - Lightweight endpoint for sale modal infinite scroll
        [HttpGet]
        public async Task<IActionResult> GetForSaleModal(
            int page = 1,
            int pageSize = PaginationDefaults.ModalPageSize,
            string? search = null,
            int? categoryId = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (page < 1) page = 1;
            if (pageSize < PaginationDefaults.MinPageSize) pageSize = PaginationDefaults.MinPageSize;
            if (pageSize > PaginationDefaults.MaxAllowedPageSize) pageSize = PaginationDefaults.MaxAllowedPageSize;

            var collection = _unitOfWork.Query<Product>();
            var query = collection.Where(p => p.IsActive);

            // Apply filters
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(s)) ||
                    (p.ProductCode != null && p.ProductCode.ToLower().Contains(s)));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.ProductCategoryId == categoryId.Value);
            }

            var total = query.Count();
            var skip = (page - 1) * pageSize;
            var items = query.OrderBy(p => p.Name).Skip(skip).Take(pageSize).ToList();

            // Resolve categories
            var categoryIds = items.Where(p => p.ProductCategoryId.HasValue).Select(p => p.ProductCategoryId!.Value).Distinct().ToList();
            var categories = categoryIds.Count == 0 ? new Dictionary<int, string>()
                : _unitOfWork.Query<ProductCategory>().Where(c => categoryIds.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Name);

            var data = items.Select(p => new
            {
                id = p.Id,
                productCode = p.ProductCode ?? string.Empty,
                name = p.Name ?? string.Empty,
                categoryId = p.ProductCategoryId,
                category = p.ProductCategoryId.HasValue && categories.TryGetValue(p.ProductCategoryId.Value, out var cn) ? cn : string.Empty,
                stock = p.CurrentStock,
                salePrice = p.CurrentSalePrice,
                lowStockCount = p.LowStockCount
            });

            return Json(new { items = data, hasMore = skip + items.Count < total, total });
        }

        // GET: Products/GetCategories - Returns categories for dropdown
        [HttpGet]
        public IActionResult GetCategories(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var categories = _unitOfWork.Query<ProductCategory>()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.Id, name = c.Name })
                .ToList();

            return Json(categories);
        }

        // GET: Products/Import
        [Authorize(Policy = Common.ModuleRegistry.Pages.PRODUCTS_CREATE)]
        public IActionResult Import(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        // POST: Products/Import
        // Only reads/validates the upload here — the actual row-by-row work happens on
        // ProductImportBackgroundService so a large file doesn't tie up the request, and keeps
        // running even if the uploader navigates away. The client polls ImportStatus for progress.
        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.PRODUCTS_CREATE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile csvFile, bool updateExisting = false, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (csvFile == null || csvFile.Length == 0)
            {
                return BadRequest(new { error = "Please select a CSV file to upload." });
            }

            if (!Path.GetExtension(csvFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Please upload a CSV file." });
            }

            byte[] fileContent;
            using (var ms = new MemoryStream())
            {
                await csvFile.CopyToAsync(ms, ct);
                fileContent = ms.ToArray();
            }

            var jobId = Guid.NewGuid().ToString();
            _importJobTracker.CreateJob(jobId);
            await _importQueue.EnqueueAsync(new Mercurius.Services.ProductImportJob
            {
                JobId = jobId,
                FileContent = fileContent,
                UpdateExisting = updateExisting
            }, ct);

            return Json(new { jobId });
        }

        // GET: Products/ImportStatus?jobId=...
        // Polled by the progress modal on Views/Products/Import.cshtml.
        [HttpGet]
        [Authorize(Policy = Common.ModuleRegistry.Pages.PRODUCTS_CREATE)]
        public IActionResult ImportStatus(string jobId)
        {
            var state = _importJobTracker.Get(jobId);
            if (state == null)
            {
                return NotFound();
            }

            return Json(new
            {
                status = state.Status,
                total = state.Total,
                processed = state.Processed,
                created = state.Created,
                updated = state.Updated,
                skipped = state.Skipped,
                errors = state.Errors,
                log = _importJobTracker.SnapshotLog(state),
                errorMessage = state.ErrorMessage
            });
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id, CancellationToken ct = default)
        {
            if (id == null)
            {
                return NotFound();
            }

            var products = await _unitOfWork.Repository<Product>().FindAsync(p => p.Id == id, ct);
            var product = products.FirstOrDefault();

            if (product == null)
            {
                return NotFound();
            }

            if (product.ProductCategoryId.HasValue)
            {
                var categories = await _unitOfWork.Repository<ProductCategory>()
                    .FindAsync(c => c.Id == product.ProductCategoryId.Value, ct);
                ViewBag.CategoryName = categories.FirstOrDefault()?.Name;
            }

            // Category-specific custom fields (Generic Name, Strength, etc.) — same data the
            // Create/Edit modal shows, but this page had never displayed it at all.
            if (product.ProductCategoryId.HasValue)
            {
                var categoryFields = await _unitOfWork.Repository<CategoryField>()
                    .FindAsync(f => f.CategoryId == product.ProductCategoryId.Value, ct);
                var fieldValues = await _unitOfWork.Repository<ProductField>()
                    .FindAsync(pf => pf.ProductId == product.Id, ct);
                var valueByFieldId = fieldValues.ToDictionary(pf => pf.CategoryFieldId, pf => pf.Value);

                ViewBag.CustomFields = categoryFields
                    .OrderBy(f => f.SortOrder)
                    .Select(f => new
                    {
                        f.DisplayLabel,
                        Value = valueByFieldId.TryGetValue(f.Id, out var v) ? v : null
                    })
                    .Where(f => !string.IsNullOrEmpty(f.Value))
                    .ToList();
            }

            return View(product);
        }

        // GET: Products/Create
        // Kept for backward compatibility; Index now renders the create UI as a modal,
        // so this just sends the user back to the list.
        [Authorize(Policy = Common.ModuleRegistry.Pages.PRODUCTS_CREATE)]
        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return RedirectToAction(nameof(Index));
        }

        // POST: Products/Create
        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.PRODUCTS_CREATE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? productimage, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                try
                {
                    product.CreateDate = DateTime.UtcNow;
                    product.IsActive = true;

                    var savedFile = await SaveProductImageAsync(productimage, ct);
                    if (!string.IsNullOrEmpty(savedFile))
                    {
                        product.ImageFilename = savedFile;
                    }

                    await _unitOfWork.Repository<Product>().AddAsync(product, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    await SaveCustomFieldsAsync(product.Id, product.ProductCategoryId, Request.Form, ct);
                }
                catch (Exception ex)
                {
                    _loggerService.LogError($"Create POST for Product '{product?.Name}' failed.", ex);
                    var errorMessage = $"Error creating product: {ex.Message}";
                    if (Request.WantsJson())
                    {
                        return AjaxFormResults.JsonError(errorMessage);
                    }
                    SetFlashOnView(errorMessage, "error");
                    var productsForRetry = await _unitOfWork.Repository<Product>().GetAllAsync(ct);
                    await LoadViewBags(ct);
                    return View(nameof(Index), productsForRetry);
                }

                var successMessage = $"Product '{product.Name}' saved.";
                if (Request.WantsJson())
                {
                    return AjaxFormResults.JsonOk(Url.Action(nameof(Index)) ?? "/Products", successMessage);
                }
                SetFlashForRedirect(successMessage, "success");
                return RedirectToAction(nameof(Index));
            }

            // Re-render the Index view so the modal can reopen with validation errors visible.
            LogAndFlashModelStateErrors($"Create POST for Product '{product?.Name}'");
            if (Request.WantsJson())
            {
                return AjaxFormResults.JsonError(ModelState.ToErrorList());
            }
            var products = await _unitOfWork.Repository<Product>().GetAllAsync(ct);
            await LoadViewBags(ct);
            return View(nameof(Index), products);
        }

        // GET: Products/Edit/5
        [Authorize(Policy = Common.ModuleRegistry.Pages.PRODUCTS_EDIT)]
        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            if (id == null)
            {
                return NotFound();
            }

            var products = await _unitOfWork.Repository<Product>().FindAsync(p => p.Id == id, ct);
            var product = products.FirstOrDefault();

            if (product == null)
            {
                return NotFound();
            }

            await LoadViewBags(ct);
            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.PRODUCTS_EDIT)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? productimage, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var savedFile = await SaveProductImageAsync(productimage, ct);
                    if (!string.IsNullOrEmpty(savedFile))
                    {
                        product.ImageFilename = savedFile;
                    }

                    product.UpdatedDate = DateTime.UtcNow;
                    await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    await SaveCustomFieldsAsync(product.Id, product.ProductCategoryId, Request.Form, ct);
                }
                catch (Exception ex)
                {
                    _loggerService.LogError($"Edit POST for Product Id={id} failed.", ex);
                    var errorMessage = $"Error updating product: {ex.Message}";
                    if (Request.WantsJson())
                    {
                        return AjaxFormResults.JsonError(errorMessage);
                    }
                    SetFlashOnView(errorMessage, "error");
                    await LoadViewBags(ct);
                    return View(product);
                }

                var successMessage = $"Product '{product.Name}' updated.";
                if (Request.WantsJson())
                {
                    return AjaxFormResults.JsonOk(Url.Action(nameof(Index)) ?? "/Products", successMessage);
                }
                SetFlashForRedirect(successMessage, "success");
                return RedirectToAction(nameof(Index));
            }

            LogAndFlashModelStateErrors($"Edit POST for Product Id={id}");
            if (Request.WantsJson())
            {
                return AjaxFormResults.JsonError(ModelState.ToErrorList());
            }
            await LoadViewBags(ct);
            return View(product);
        }

        // GET: Products/Delete/5
        [Authorize(Policy = Common.ModuleRegistry.Pages.PRODUCTS_DELETE)]
        public async Task<IActionResult> Delete(int? id, CancellationToken ct = default)
        {
            if (id == null)
            {
                return NotFound();
            }

            var products = await _unitOfWork.Repository<Product>().FindAsync(p => p.Id == id, ct);
            var product = products.FirstOrDefault();

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Policy = Common.ModuleRegistry.Pages.PRODUCTS_DELETE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var products = await _unitOfWork.Repository<Product>().FindAsync(p => p.Id == id, ct);
            var product = products.FirstOrDefault();

            if (product != null)
            {
                await _unitOfWork.Repository<Product>().DeleteAsync(product.Id, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Products/QRCode/5
        public async Task<IActionResult> QRCode(int? id, CancellationToken ct = default)
        {
            if (id == null)
            {
                return NotFound();
            }

            var products = await _unitOfWork.Repository<Product>().FindAsync(p => p.Id == id, ct);
            var product = products.FirstOrDefault();

            if (product == null)
            {
                return NotFound();
            }

            var qrCodeData = $"ProductCode:{product.ProductCode}|Name:{product.Name}";
            var qr = QrCode.EncodeText(qrCodeData, QrCode.Ecc.Medium);

            var svg = qr.ToSvgString(border: 4);
            return File(System.Text.Encoding.UTF8.GetBytes(svg), "image/svg+xml");
        }

        private async Task LoadViewBags(CancellationToken ct = default)
        {
            var categories = await _unitOfWork.Repository<ProductCategory>().GetAllAsync(ct);
            ViewBag.ProductCategoryId = new SelectList(categories, "Id", "Name");
        }

        /// <summary>
        /// Returns custom field definitions for a category, optionally with
        /// existing values for a specific product (for edit forms).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCategoryFields(int categoryId, int? productId = null, CancellationToken ct = default)
        {
            var fields = await _unitOfWork.Repository<CategoryField>()
                .FindAsync(f => f.CategoryId == categoryId, ct);

            // Fetch existing values if editing an existing product
            Dictionary<int, string>? existingValues = null;
            if (productId.HasValue)
            {
                var values = await _unitOfWork.Repository<ProductField>()
                    .FindAsync(pf => pf.ProductId == productId.Value, ct);
                existingValues = values.ToDictionary(pf => pf.CategoryFieldId, pf => pf.Value);
            }

            var result = fields.OrderBy(f => f.SortOrder).Select(f => new
            {
                f.Id,
                f.FieldName,
                f.DisplayLabel,
                f.FieldType,
                options = string.IsNullOrEmpty(f.Options)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<string[]>(f.Options),
                f.IsRequired,
                value = existingValues != null && existingValues.TryGetValue(f.Id, out var v) ? v : null
            });

            return Json(result);
        }

        /// <summary>
        /// Saves custom field values for a product. Called after create/update.
        /// Replaces all existing custom fields for the given product.
        /// Field names are only unique within a category, so the lookup must be
        /// scoped to the product's current category — building a global dictionary
        /// keyed by FieldName throws when two categories define the same field name
        /// (e.g., both Medicines and Supplements have "GenericName").
        /// </summary>
        private async Task SaveCustomFieldsAsync(int productId, int? categoryId, IFormCollection form, CancellationToken ct = default)
        {
            var repo = _unitOfWork.Repository<ProductField>();

            // Always delete existing values, even if the product no longer has a category
            // (so old category fields don't linger after a category change).
            var existing = await repo.FindAsync(pf => pf.ProductId == productId, ct);
            foreach (var pf in existing)
            {
                await repo.DeleteAsync(pf.Id, ct);
            }

            if (!categoryId.HasValue)
            {
                await _unitOfWork.SaveChangesAsync(ct);
                return;
            }

            var categoryFields = await _unitOfWork.Repository<CategoryField>()
                .FindAsync(f => f.CategoryId == categoryId.Value, ct);

            // Within a single category, FieldName should be unique. If two definitions
            // collide we keep the first and ignore the rest rather than throwing.
            var fieldDict = new Dictionary<string, CategoryField>(StringComparer.Ordinal);
            foreach (var f in categoryFields)
            {
                if (!string.IsNullOrEmpty(f.FieldName) && !fieldDict.ContainsKey(f.FieldName))
                {
                    fieldDict[f.FieldName] = f;
                }
            }

            // Insert new values from form data
            foreach (var key in form.Keys)
            {
                if (key.StartsWith("cf_") && fieldDict.TryGetValue(key[3..], out var field))
                {
                    var value = form[key].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        await repo.AddAsync(new ProductField
                        {
                            ProductId = productId,
                            CategoryFieldId = field.Id,
                            Value = value
                        }, ct);
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Saves an uploaded product image to wwwroot/ProductImages and returns the stored filename.
        /// Returns null/empty if no file was uploaded.
        /// </summary>
        private async Task<string?> SaveProductImageAsync(IFormFile? file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var allowed = new[] { ".png", ".jpg", ".jpeg" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
            {
                return null;
            }

            var webRoot = _webHostEnvironment.WebRootPath;
            var folder = Path.Combine(webRoot, "ProductImages");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var storedName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, storedName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, ct);
            }

            return storedName;
        }

        /// <summary>
        /// Stores a flash message that will be picked up by _Layout.cshtml after a redirect
        /// and rendered as a toastr notification.
        /// </summary>
        private void SetFlashForRedirect(string message, string type)
        {
            TempData["FlashMessage"] = message;
            TempData["FlashType"] = type;
        }

        /// <summary>
        /// Same as SetFlashForRedirect, but for when we re-render the same view (e.g. validation
        /// failure) instead of redirecting — TempData would survive too long in that case.
        /// </summary>
        private void SetFlashOnView(string message, string type)
        {
            ViewData["FlashMessage"] = message;
            ViewData["FlashType"] = type;
        }

        /// <summary>
        /// Collects ModelState errors into a human-readable string, logs them, and surfaces
        /// them as an error toast on the re-rendered view. Used by Create/Edit POST when
        /// validation fails so the user sees what was actually wrong.
        /// </summary>
        private void LogAndFlashModelStateErrors(string contextPrefix)
        {
            var entries = ModelState
                .Where(kv => kv.Value != null && kv.Value.Errors.Count > 0)
                .Select(kv => new
                {
                    Field = string.IsNullOrEmpty(kv.Key) ? "(form)" : kv.Key,
                    Messages = kv.Value!.Errors.Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m))
                })
                .ToList();

            if (entries.Count == 0)
            {
                return;
            }

            var logSummary = string.Join("; ", entries.Select(e => $"{e.Field}: {string.Join(", ", e.Messages)}"));
            _loggerService.LogWarning($"{contextPrefix} failed model validation. {logSummary}");

            var toastSummary = string.Join(" • ", entries.Select(e =>
            {
                var msgs = string.Join(", ", e.Messages);
                return string.IsNullOrEmpty(msgs) ? e.Field : $"{e.Field}: {msgs}";
            }));
            SetFlashOnView($"Please fix the following: {toastSummary}", "error");
        }
    }
}