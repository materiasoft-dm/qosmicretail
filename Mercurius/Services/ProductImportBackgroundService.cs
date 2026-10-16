using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Models;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mercurius.Services
{
    /// <summary>
    /// Processes queued CSV product imports outside the HTTP request that uploaded them, so a
    /// large file doesn't block on the request/connection lifetime and keeps running even if the
    /// uploader navigates away. Progress is reported through ImportJobTracker for the UI to poll.
    /// </summary>
    public class ProductImportBackgroundService : BackgroundService
    {
        private readonly ProductImportQueue _queue;
        private readonly ImportJobTracker _tracker;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ProductImportBackgroundService> _logger;

        public ProductImportBackgroundService(
            ProductImportQueue queue,
            ImportJobTracker tracker,
            IServiceScopeFactory scopeFactory,
            ILogger<ProductImportBackgroundService> logger)
        {
            _queue = queue;
            _tracker = tracker;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessAsync(job, stoppingToken);
                }
                catch (Exception ex)
                {
                    var state = _tracker.Get(job.JobId);
                    if (state != null)
                    {
                        state.Status = "Failed";
                        state.ErrorMessage = ex.Message;
                        state.CompletedAt = DateTime.UtcNow;
                    }
                    _tracker.AppendLog(job.JobId, $"Import failed: {ex.Message}");
                    _logger.LogError(ex, "Product import job {JobId} failed", job.JobId);
                }
            }
        }

        private async Task ProcessAsync(ProductImportJob job, CancellationToken ct)
        {
            var state = _tracker.Get(job.JobId);
            if (state == null) return;

            state.Status = "Parsing";
            _tracker.AppendLog(job.JobId, "Parsing CSV file...");

            using var scope = _scopeFactory.CreateScope();
            // Act as the uploader's tenant: there's no HttpContext here, so without this every
            // query saw nothing and every created product got no real tenant (invisible).
            if (job.TenantId > 0)
            {
                scope.ServiceProvider.GetRequiredService<ITenantScopeOverride>().UseTenant(job.TenantId);
            }
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Loyverse (and similar POS) exports decorate several headers with a per-location
            // suffix, e.g. "Price [Store Name]" / "In stock [Store Name]" instead of a plain
            // "Price"/"In stock". ProductCsvMap's candidate names (and its own comments) were
            // written assuming that suffix gets stripped before matching, but nothing actually
            // configured CsvHelper to do that — every row silently fell through to the mapper's
            // Default(0m), which is why a real Loyverse export with real prices imported every
            // product at ₱0.00 sale price with no error. PrepareHeaderForMatch runs on both the
            // file's header text and the candidate names in ProductCsvMap.Name(...), so stripping
            // the bracket suffix and normalizing whitespace/case here — instead of also editing
            // every candidate name — is enough to make "Price [Store Name]" match "Price" and
            // "In stock [Store Name]" match "Instock".
            var csvConfig = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
            {
                // Ignore missing headers — use default values (0 for price/stock) when columns absent
                HeaderValidated = null,
                MissingFieldFound = null,
                PrepareHeaderForMatch = args => Regex.Replace(args.Header, @"\s*\[[^\]]*\]\s*$", "")
                    .Replace(" ", "")
                    .ToLowerInvariant()
            };

            System.Collections.Generic.List<ProductCsvRecord> records;
            using (var ms = new MemoryStream(job.FileContent))
            using (var reader = new StreamReader(ms))
            using (var csv = new CsvHelper.CsvReader(reader, csvConfig))
            {
                csv.Context.RegisterClassMap<ProductCsvMap>();
                records = csv.GetRecords<ProductCsvRecord>().ToList();
            }

            state.Total = records.Count;
            state.Status = "Importing";
            _tracker.AppendLog(job.JobId, $"Parsed {records.Count} rows. Starting import...");

            // Stock from the file is recorded as adjustments (reason "Stock import"), resolved
            // lazily: a file with no stock never creates the reason.
            var adjustments = scope.ServiceProvider.GetRequiredService<AdjustmentService>();
            AdjustmentReason? stockReason = null;
            int? locationId = null;

            var rowNum = 1;
            foreach (var record in records)
            {
                rowNum++;
                try
                {
                    var existingProducts = await unitOfWork.Repository<Product>()
                        .FindAsync(p => p.ProductCode == record.ProductCode, ct);
                    var existingProduct = existingProducts.FirstOrDefault();

                    if (existingProduct != null && !job.UpdateExisting)
                    {
                        state.Skipped++;
                    }
                    else
                    {
                        ProductCategory? category = null;
                        if (!string.IsNullOrEmpty(record.Category))
                        {
                            var categories = await unitOfWork.Repository<ProductCategory>()
                                .FindAsync(c => c.Name == record.Category, ct);
                            category = categories.FirstOrDefault();

                            if (category == null)
                            {
                                category = new ProductCategory { Name = record.Category };
                                await unitOfWork.Repository<ProductCategory>().AddAsync(category, ct);
                                await unitOfWork.SaveChangesAsync(ct);
                            }
                        }

                        var barcode = string.IsNullOrWhiteSpace(record.Barcode) ? null : record.Barcode.Trim();
                        Product product;
                        decimal stockChange;
                        string adjustmentNote;
                        if (existingProduct != null)
                        {
                            existingProduct.Name = record.Name ?? existingProduct.Name;
                            existingProduct.Description = record.Description ?? existingProduct.Description;
                            existingProduct.CurrentCostPrice = record.CostPrice;
                            existingProduct.CurrentSalePrice = record.SalePrice;
                            existingProduct.ProductCategoryId = category?.Id ?? existingProduct.ProductCategoryId;
                            existingProduct.Barcode = barcode ?? existingProduct.Barcode;
                            existingProduct.LowStockCount = record.LowStock ?? existingProduct.LowStockCount;
                            existingProduct.UpdatedDate = DateTime.UtcNow;

                            await unitOfWork.Repository<Product>().UpdateAsync(existingProduct, ct);
                            product = existingProduct;
                            // Only the difference moves stock, so the adjustment history adds up.
                            stockChange = record.StockQuantity - existingProduct.CurrentStock;
                            adjustmentNote = $"Stock count from CSV import (was {existingProduct.CurrentStock:0.##})";
                            state.Updated++;
                        }
                        else
                        {
                            // Created with no stock; the file's quantity is recorded as an
                            // adjustment below, so opening stock shows up in the history.
                            product = new Product
                            {
                                ProductCode = record.ProductCode ?? "",
                                Name = record.Name ?? "",
                                Description = record.Description ?? "",
                                CurrentCostPrice = record.CostPrice,
                                CurrentSalePrice = record.SalePrice,
                                CurrentStock = 0,
                                Barcode = barcode,
                                LowStockCount = record.LowStock ?? 0,
                                ProductCategoryId = category?.Id,
                                IsActive = true,
                                CreateDate = DateTime.UtcNow
                            };

                            await unitOfWork.Repository<Product>().AddAsync(product, ct);
                            stockChange = record.StockQuantity;
                            adjustmentNote = "Opening stock from CSV import";
                            state.Created++;
                        }

                        await unitOfWork.SaveChangesAsync(ct);

                        if (stockChange != 0)
                        {
                            stockReason ??= await GetOrCreateStockImportReasonAsync(unitOfWork, ct);
                            locationId ??= job.LocationId > 0 ? job.LocationId
                                : (await unitOfWork.Repository<Location>().GetAllAsync(ct)).Select(l => l.Id).DefaultIfEmpty(0).Min();
                            // The reason is inbound; a negative count (stock short of zero) is a
                            // negative quantity, which AdjustmentService subtracts.
                            var adjustment = new Adjustment
                            {
                                AdjustmentDate = DateTime.UtcNow,
                                ReasonId = stockReason.Id,
                                Quantity = stockChange,
                                ProductId = product.Id,
                                LocationId = locationId.Value,
                                Note = adjustmentNote,
                                CreatedBy = job.UserId,
                                CreatedDate = DateTime.UtcNow,
                                IsActive = true
                            };
                            await unitOfWork.Repository<Adjustment>().AddAsync(adjustment, ct);
                            await unitOfWork.SaveChangesAsync(ct);
                            await adjustments.ApplyAsync(adjustment, ct);
                            state.StockAdjusted++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    state.Errors++;
                    _tracker.AppendLog(job.JobId, $"Row {rowNum}: {record?.ProductCode} — Error: {ex.Message}");
                    _logger.LogError(ex, "CSV import row failed for ProductCode '{ProductCode}' (job {JobId})", record?.ProductCode, job.JobId);
                }

                state.Processed++;
                if (state.Processed % 50 == 0 || state.Processed == state.Total)
                {
                    _tracker.AppendLog(job.JobId, $"Importing {state.Processed}/{state.Total} products...");
                }
            }

            state.Status = "Completed";
            state.CompletedAt = DateTime.UtcNow;
            _tracker.AppendLog(job.JobId,
                $"Done. Created {state.Created}, Updated {state.Updated}, Skipped {state.Skipped}, Errors {state.Errors}. Stock adjustments: {state.StockAdjusted}.");
        }

        // Same get-or-create pattern as the refund restock reason (InvoicesApiController).
        private static async Task<AdjustmentReason> GetOrCreateStockImportReasonAsync(IUnitOfWork unitOfWork, CancellationToken ct)
        {
            var found = (await unitOfWork.Repository<AdjustmentReason>().FindAsync(r => r.Name == "Stock import", ct)).FirstOrDefault();
            if (found != null) return found;

            var created = new AdjustmentReason
            {
                Name = "Stock import",
                Description = "Stock quantities loaded from a product CSV import.",
                IsActive = true,
                IsInbound = true
            };
            await unitOfWork.Repository<AdjustmentReason>().AddAsync(created, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return created;
        }
    }
}
