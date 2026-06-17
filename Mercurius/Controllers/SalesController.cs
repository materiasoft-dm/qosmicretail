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

        public SalesController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
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
        public async Task<IActionResult> NewSale(int customerId, string notes, List<string> items, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            // Customer is optional - customerId can be 0
            Customer? customer = null;
            if (customerId > 0)
            {
                var customers = await _unitOfWork.Repository<Customer>().FindAsync(c => c.Id == customerId, ct);
                customer = customers.FirstOrDefault();
                if (customer == null)
                {
                    ModelState.AddModelError(string.Empty, "Selected customer not found.");
                    return View();
                }
            }

            var invoice = new Invoice
            {
                CustomerId = customerId > 0 ? customerId : (int?)null,
                StatusId = (int)StatusCollection.InvoiceStatus.Draft,
                InvoiceDate = DateTime.UtcNow,
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Notes = notes,
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

            await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                await _unitOfWork.Repository<Invoice>().AddAsync(invoice, ct);

                // Get current user info for audit log
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userName = User.Identity?.Name ?? "Unknown";

                // Add invoice items
                if (items != null && items.Any())
                {
                    foreach (var item in items)
                    {
                        var parts = item.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int productId) && int.TryParse(parts[1], out int qty))
                        {
                            if (productMap.TryGetValue(productId, out var product))
                            {
                                var invoiceItem = new InvoiceItem
                                {
                                    InvoiceId = invoice.Id,
                                    ProductId = productId,
                                    Quantity = qty,
                                    SalePrice = product.CurrentSalePrice ?? 0,
                                    CostPrice = product.CurrentCostPrice ?? 0,
                                    StatusId = (int)StatusCollection.InvoiceStatus.Draft
                                };
                                await _unitOfWork.Repository<InvoiceItem>().AddAsync(invoiceItem, ct);

                                // Audit log for zero-stock sales
                                if (product.CurrentStock <= 0)
                                {
                                    var auditLog = new ZeroStockSaleAuditLog
                                    {
                                        ProductId = product.Id,
                                        ProductName = product.Name,
                                        ProductPartCode = product.PartCode,
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
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }

            TempData["Message"] = $"Invoice #{invoice.InvoiceNumber} created with items.";
            return RedirectToAction("Index", "InvoiceList");
        }

    }
}