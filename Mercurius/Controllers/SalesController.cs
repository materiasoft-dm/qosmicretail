using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using System.Security.Claims;

namespace Mercurius.Controllers
{
    [Authorize]
    public class SalesController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.BatchPricingService _batchPricingService;

        public SalesController(
            IHttpContextAccessor httpContextAccessor,
            IUnitOfWork unitOfWork,
            Mercurius.Services.BatchPricingService batchPricingService)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _batchPricingService = batchPricingService;
        }

        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return RedirectToAction("Index", "InvoiceList");
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.NEWSALE_CREATE)]
        public IActionResult NewSale(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            // Customers and products are loaded via AJAX by the view (paginated endpoints).
            // No data is fetched on initial page load.
            return View();
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.NEWSALE_CREATE)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewSale(int customerId, string notes, List<string> items, string paymentMethod, decimal amountReceived, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (items == null || items.Count == 0)
            {
                return BadRequest(new { error = "Add at least one item before charging." });
            }

            // Customer is optional - customerId can be 0
            Customer? customer = null;
            if (customerId > 0)
            {
                var customers = await _unitOfWork.Repository<Customer>().FindAsync(c => c.Id == customerId, ct);
                customer = customers.FirstOrDefault();
                if (customer == null)
                {
                    return BadRequest(new { error = "Selected customer not found." });
                }
            }

            // Mobile's checkout has no separate notes field — it just stamps Invoice.Notes with
            // "Paid by cash"/"Paid by card" (see SalesPage.xaml.cs's OnChargeClicked). Web already
            // has a free-text cashier note, so fold the two together instead of losing one: use
            // the payment annotation alone when the cashier left Notes blank (matching mobile
            // exactly), otherwise append it to what they typed.
            var isCard = string.Equals(paymentMethod, "Card", StringComparison.OrdinalIgnoreCase);
            var paymentNote = isCard ? "Paid by card" : "Paid by cash";
            var resolvedNotes = string.IsNullOrWhiteSpace(notes) ? paymentNote : $"{notes} ({paymentNote})";

            var invoice = new Invoice
            {
                CustomerId = customerId > 0 ? customerId : (int?)null,
                StatusId = (int)StatusCollection.InvoiceStatus.Draft,
                InvoiceDate = DateTime.UtcNow,
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                Notes = resolvedNotes,
                PaidAmount = amountReceived,
                CreatedDate = DateTime.UtcNow
            };

            // Fetch all products first (before transaction to avoid lock issues)
            var productMap = new Dictionary<int, Product>();
            if (items != null && items.Any())
            {
                var productIds = items
                    .Select(item => item.Split(':')[0])
                    .Where(id => int.TryParse(id, out _))
                    .Select(id => int.Parse(id))
                    .Distinct()
                    .ToList();

                if (productIds.Any())
                {
                    var products = await _unitOfWork.Repository<Product>().FindAsync(p => productIds.Contains(p.Id), ct);
                    foreach (var product in products)
                    {
                        productMap[product.Id] = product;
                    }
                }
            }

            var grandTotal = 0m;
            await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                await _unitOfWork.Repository<Invoice>().AddAsync(invoice, ct);
                // Flush immediately so invoice.Id holds the real database-generated value before
                // it's read below — EF Core only assigns a temporary placeholder key synchronously
                // on Add(), and reading that into a plain scalar FK (rather than a tracked
                // navigation) would otherwise insert invoice items pointing at a value that never
                // matches any real Invoice row.
                await _unitOfWork.SaveChangesAsync(ct);

                // Get current user info for audit log
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userName = User.Identity?.Name ?? "Unknown";

                // Add invoice items
                var productsWithBatchActivity = new HashSet<int>();
                if (items != null && items.Any())
                {
                    foreach (var item in items)
                    {
                        var parts = item.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int productId) && int.TryParse(parts[1], out int qty))
                        {
                            if (productMap.TryGetValue(productId, out var product))
                            {
                                // Batch-tracked products (see MedicineBatch) price and deduct from
                                // whichever batch covers the full line quantity, oldest first — a
                                // product with no batches at all falls back to its flat price
                                // fields exactly as before.
                                var batch = await _batchPricingService.FindFulfillingBatchAsync(_unitOfWork, productId, qty, ct);

                                var invoiceItem = new InvoiceItem
                                {
                                    InvoiceId = invoice.Id,
                                    ProductId = productId,
                                    Quantity = qty,
                                    SalePrice = batch?.UnitSalePrice ?? product.CurrentSalePrice ?? 0,
                                    CostPrice = batch?.UnitCost ?? product.CurrentCostPrice ?? 0,
                                    MedicineBatchId = batch?.Id,
                                    StatusId = (int)StatusCollection.InvoiceStatus.Draft
                                };
                                await _unitOfWork.Repository<InvoiceItem>().AddAsync(invoiceItem, ct);
                                grandTotal += invoiceItem.SalePrice * invoiceItem.Quantity;

                                // Stock deducts for every sale, batch-tracked or not — only the
                                // batch ledger itself is conditional on a batch actually being
                                // found. Mirrors SyncController.PushInvoices, which already got
                                // this right; this action used to nest the stock deduction inside
                                // the `if (batch != null)` block too, so non-batch products (most
                                // of the catalog) never had CurrentStock reduced by a sale at all.
                                if (batch != null)
                                {
                                    batch.RemainingQuantity -= qty;
                                    await _unitOfWork.Repository<MedicineBatch>().UpdateAsync(batch, ct);
                                }
                                product.CurrentStock -= qty;
                                await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
                                productsWithBatchActivity.Add(productId);

                                // Audit log for zero-stock sales
                                if (product.CurrentStock <= 0)
                                {
                                    var auditLog = new ZeroStockSaleAuditLog
                                    {
                                        ProductId = product.Id,
                                        ProductName = product.Name,
                                        ProductCode = product.ProductCode,
                                        QuantitySold = qty,
                                        StockAtTimeOfSale = product.CurrentStock,
                                        InvoiceNumber = invoice.InvoiceNumber,
                                        InvoiceId = invoice.Id,
                                        SoldByUserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
                                        SoldByUserName = userName,
                                        SaleDate = DateTime.UtcNow,
                                        Notes = $"Zero-stock sale: inventory was {product.CurrentStock} before this sale of {qty} units"
                                    };
                                    await _unitOfWork.Repository<ZeroStockSaleAuditLog>().AddAsync(auditLog, ct);
                                }
                            }
                        }
                    }
                    await _unitOfWork.SaveChangesAsync(ct);
                }

                await _unitOfWork.CommitTransactionAsync(ct);

                // Outside the transaction (it's a read-then-write derived from what was just
                // committed): roll the product's displayed price over to the next batch if this
                // sale exactly depleted the one that was active.
                foreach (var productId in productsWithBatchActivity)
                {
                    await _batchPricingService.RefreshActivePriceAsync(_unitOfWork, productId, ct);
                }
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }

            return Json(new
            {
                success = true,
                invoiceNumber = invoice.InvoiceNumber,
                invoiceId = invoice.Id,
                total = grandTotal,
                change = isCard ? 0m : amountReceived - grandTotal
            });
        }

    }
}