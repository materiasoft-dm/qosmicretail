using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            System.Collections.Generic.List<ProductCsvRecord> records;
            using (var ms = new MemoryStream(job.FileContent))
            using (var reader = new StreamReader(ms))
            using (var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<ProductCsvMap>();
                // Ignore missing headers — use default values (0 for price/stock) when columns absent
                csv.Context.Configuration.HeaderValidated = null;
                csv.Context.Configuration.MissingFieldFound = null;
                records = csv.GetRecords<ProductCsvRecord>().ToList();
            }

            state.Total = records.Count;
            state.Status = "Importing";
            _tracker.AppendLog(job.JobId, $"Parsed {records.Count} rows. Starting import...");

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

                        if (existingProduct != null)
                        {
                            existingProduct.Name = record.Name ?? existingProduct.Name;
                            existingProduct.Description = record.Description ?? existingProduct.Description;
                            existingProduct.CurrentCostPrice = record.CostPrice;
                            existingProduct.CurrentSalePrice = record.SalePrice;
                            existingProduct.CurrentStock = record.StockQuantity;
                            existingProduct.ProductCategoryId = category?.Id ?? existingProduct.ProductCategoryId;
                            existingProduct.UpdatedDate = DateTime.UtcNow;

                            await unitOfWork.Repository<Product>().UpdateAsync(existingProduct, ct);
                            state.Updated++;
                        }
                        else
                        {
                            var newProduct = new Product
                            {
                                ProductCode = record.ProductCode ?? "",
                                Name = record.Name ?? "",
                                Description = record.Description ?? "",
                                CurrentCostPrice = record.CostPrice,
                                CurrentSalePrice = record.SalePrice,
                                CurrentStock = record.StockQuantity,
                                ProductCategoryId = category?.Id,
                                IsActive = true,
                                CreateDate = DateTime.UtcNow
                            };

                            await unitOfWork.Repository<Product>().AddAsync(newProduct, ct);
                            state.Created++;
                        }

                        await unitOfWork.SaveChangesAsync(ct);
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
                $"Done. Created {state.Created}, Updated {state.Updated}, Skipped {state.Skipped}, Errors {state.Errors}.");
        }
    }
}
