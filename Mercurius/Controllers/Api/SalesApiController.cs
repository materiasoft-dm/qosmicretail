using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // JSON equivalent of SalesController.NewSale's POST action — logic copied line-for-line
    // (batch pricing, unconditional stock deduction, zero-stock audit log, transaction handling)
    // rather than having the Blazor page call the MVC action directly, since that action expects
    // form-encoded data plus an antiforgery token embedded in server-rendered HTML, neither of
    // which a Blazor WASM SPA naturally has. Once the old MVC Sales/NewSale page is retired, that
    // action can be deleted and this becomes the single checkout implementation.
    [ApiController]
    [Route("api/sales")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.NEWSALE_CREATE)]
    public class SalesApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.BatchPricingService _batchPricingService;

        public SalesApiController(IUnitOfWork unitOfWork, Mercurius.Services.BatchPricingService batchPricingService)
        {
            _unitOfWork = unitOfWork;
            _batchPricingService = batchPricingService;
        }

        [HttpGet("customers")]
        public ActionResult<List<CustomerOptionDto>> GetCustomers(string? search = null)
        {
            var query = _unitOfWork.Query<Customer>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(c => (c.FirstName != null && c.FirstName.ToLower().Contains(s)) || (c.LastName != null && c.LastName.ToLower().Contains(s)));
            }
            var items = query.OrderBy(c => c.FirstName).Take(50)
                .Select(c => new CustomerOptionDto { Id = c.Id, Name = ((c.FirstName ?? "") + " " + (c.LastName ?? "")).Trim() }).ToList();
            return Ok(items);
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout(CheckoutRequest request, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (request.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { error = "Add at least one item before charging." });
            }

            Customer? customer = null;
            if (request.CustomerId > 0)
            {
                customer = (await _unitOfWork.Repository<Customer>().FindAsync(c => c.Id == request.CustomerId, ct)).FirstOrDefault();
                if (customer == null) return BadRequest(new { error = "Selected customer not found." });
            }

            var isCard = string.Equals(request.PaymentMethod, "Card", StringComparison.OrdinalIgnoreCase);
            var paymentNote = isCard ? "Paid by card" : "Paid by cash";
            var resolvedNotes = string.IsNullOrWhiteSpace(request.Notes) ? paymentNote : $"{request.Notes} ({paymentNote})";

            var locationId = await Mercurius.ViewComponents.Dashboard.DashboardLocationContext.GetCurrentLocationIdAsync(_unitOfWork, User);

            var invoice = new Invoice
            {
                CustomerId = request.CustomerId > 0 ? request.CustomerId : (int?)null,
                LocationId = locationId,
                StatusId = (int)StatusCollection.InvoiceStatus.Draft,
                InvoiceDate = DateTime.UtcNow,
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                Notes = resolvedNotes,
                PaidAmount = request.AmountReceived,
                CreatedDate = DateTime.UtcNow
            };

            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var productMap = (await _unitOfWork.Repository<Product>().FindAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);

            var grandTotal = 0m;
            await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                await _unitOfWork.Repository<Invoice>().AddAsync(invoice, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userName = User.Identity?.Name ?? "Unknown";

                var productsWithBatchActivity = new HashSet<int>();
                foreach (var line in request.Items)
                {
                    if (line.Quantity <= 0 || !productMap.TryGetValue(line.ProductId, out var product)) continue;

                    var batch = await _batchPricingService.FindFulfillingBatchAsync(_unitOfWork, line.ProductId, line.Quantity, ct);

                    var invoiceItem = new InvoiceItem
                    {
                        InvoiceId = invoice.Id,
                        ProductId = line.ProductId,
                        Quantity = line.Quantity,
                        SalePrice = batch?.UnitSalePrice ?? product.CurrentSalePrice ?? 0,
                        CostPrice = batch?.UnitCost ?? product.CurrentCostPrice ?? 0,
                        MedicineBatchId = batch?.Id,
                        StatusId = (int)StatusCollection.InvoiceStatus.Draft
                    };
                    await _unitOfWork.Repository<InvoiceItem>().AddAsync(invoiceItem, ct);
                    grandTotal += invoiceItem.SalePrice * invoiceItem.Quantity;

                    if (batch != null)
                    {
                        batch.RemainingQuantity -= line.Quantity;
                        await _unitOfWork.Repository<MedicineBatch>().UpdateAsync(batch, ct);
                    }
                    product.CurrentStock -= line.Quantity;
                    await _unitOfWork.Repository<Product>().UpdateAsync(product, ct);
                    productsWithBatchActivity.Add(line.ProductId);

                    if (product.CurrentStock <= 0)
                    {
                        await _unitOfWork.Repository<ZeroStockSaleAuditLog>().AddAsync(new ZeroStockSaleAuditLog
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            ProductCode = product.ProductCode,
                            QuantitySold = line.Quantity,
                            StockAtTimeOfSale = product.CurrentStock,
                            InvoiceNumber = invoice.InvoiceNumber,
                            InvoiceId = invoice.Id,
                            SoldByUserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
                            SoldByUserName = userName,
                            SaleDate = DateTime.UtcNow,
                            Notes = $"Zero-stock sale: inventory was {product.CurrentStock} before this sale of {line.Quantity} units"
                        }, ct);
                    }
                }
                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.CommitTransactionAsync(ct);

                foreach (var productId in productsWithBatchActivity)
                {
                    await _batchPricingService.RefreshActivePriceAsync(_unitOfWork, productId, ct);
                }
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                throw;
            }

            return Ok(new CheckoutResultDto
            {
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceId = invoice.Id,
                Total = grandTotal,
                Change = isCard ? 0m : request.AmountReceived - grandTotal
            });
        }
    }
}
