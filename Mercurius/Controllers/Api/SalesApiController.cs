using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Services;
using Mercurius.Shared.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // Web checkout (the Sell page), including sales the page queued while offline. Batch pricing,
    // stock deduction and the zero-stock audit log go through SaleRecorder, shared with
    // SyncController.PushInvoices (the mobile app's equivalent); each sale is one transaction.
    [ApiController]
    [Route("api/sales")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.NEWSALE_CREATE)]
    public class SalesApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly SaleRecorder _saleRecorder;

        public SalesApiController(IUnitOfWork unitOfWork, SaleRecorder saleRecorder)
        {
            _unitOfWork = unitOfWork;
            _saleRecorder = saleRecorder;
        }

        [HttpGet("customers")]
        public ActionResult<List<CustomerOptionDto>> GetCustomers(string? search = null)
        {
            // Inactive customers are kept on record but not offered at checkout.
            var query = _unitOfWork.Query<Customer>().Where(c => c.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(c => (c.FirstName != null && c.FirstName.ToLower().Contains(s))
                    || (c.LastName != null && c.LastName.ToLower().Contains(s))
                    || (c.ContactNumber != null && c.ContactNumber.Contains(s)));
            }
            var items = query.OrderBy(c => c.FirstName).Take(50)
                .Select(c => new CustomerOptionDto { Id = c.Id, Name = ((c.FirstName ?? "") + " " + (c.LastName ?? "")).Trim() }).ToList();
            return Ok(items);
        }

        [HttpGet("offline-catalog")]
        public ActionResult<List<OfflineProductDto>> GetOfflineCatalog(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var items = _unitOfWork.Query<Product>()
                .Where(p => p.IsActive)
                .Select(p => new OfflineProductDto
                {
                    Id = p.Id,
                    ProductCode = p.ProductCode ?? string.Empty,
                    Barcode = p.Barcode,
                    Name = p.Name ?? string.Empty,
                    CategoryId = p.ProductCategoryId,
                    Category = p.ProductCategory != null ? p.ProductCategory.Name : null,
                    SalePrice = p.CurrentSalePrice,
                    Stock = p.CurrentStock,
                    LowStockCount = p.LowStockCount
                })
                .ToList();
            return Ok(items);
        }

        // Cashier's device saved this sale locally while offline (see Mercurius.Client's
        // OfflineSyncService) and is now replaying it. Idempotent by SyncId — matching
        // Api/SyncController.PushInvoices' contract for the mobile app — so a retried sync
        // (e.g. a flaky reconnect) never double-charges inventory for the same sale.
        [HttpPost("offline-sync")]
        public async Task<IActionResult> SyncOfflineSale(OfflineSaleRequest request, CancellationToken ct = default)
        {
            if (request.SyncId is not Guid syncId || syncId == Guid.Empty)
            {
                return BadRequest(new { error = "An offline sale needs its SyncId." });
            }
            var existing = await FindExistingSaleAsync(syncId, ct);
            if (existing != null) return Ok(existing);

            // The customer already paid the ticket's prices, so they're kept, same as a mobile sale.
            return await ProcessCheckoutAsync(request, syncId, request.CreatedAtUtc, keepTicketPrices: true, ct);
        }

        // Idempotent by SyncId too: the Sell page picks one per sale, so a retry after a lost
        // response (or the same sale later replayed from the offline queue) returns the sale
        // already made instead of charging it twice.
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout(CheckoutRequest request, CancellationToken ct = default)
        {
            if (request.SyncId is Guid syncId && syncId != Guid.Empty)
            {
                var existing = await FindExistingSaleAsync(syncId, ct);
                if (existing != null) return Ok(existing);
                return await ProcessCheckoutAsync(request, syncId, DateTime.UtcNow, keepTicketPrices: false, ct);
            }
            return await ProcessCheckoutAsync(request, Guid.NewGuid(), DateTime.UtcNow, keepTicketPrices: false, ct);
        }

        private async Task<CheckoutResultDto?> FindExistingSaleAsync(Guid syncId, CancellationToken ct)
        {
            var invoice = (await _unitOfWork.Repository<Invoice>().FindAsync(i => i.SyncId == syncId, ct)).FirstOrDefault();
            if (invoice == null) return null;
            var items = await _unitOfWork.Repository<InvoiceItem>().FindAsync(ii => ii.InvoiceId == invoice.Id, ct);
            var total = items.Sum(ii => ii.SalePrice * ii.Quantity);
            var paidByCard = PaymentMethods.Normalize(invoice.PaymentMethod, invoice.Notes) == PaymentMethods.Card;
            return new CheckoutResultDto
            {
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceId = invoice.Id,
                Total = total,
                Change = paidByCard ? 0m : invoice.PaidAmount - total
            };
        }

        // keepTicketPrices: charge what the ticket showed (an offline sale the customer already
        // paid). Otherwise the server's price is charged, and a ticket showing anything else is
        // turned back with the current prices before anything is saved.
        private async Task<IActionResult> ProcessCheckoutAsync(CheckoutRequest request, Guid syncId, DateTime invoiceDateUtc, bool keepTicketPrices, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (request.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { error = "Add at least one item before charging." });
            }
            if (request.Items.Any(i => i.Quantity <= 0))
            {
                return BadRequest(new { error = "Every item needs a quantity of at least 1." });
            }

            Customer? customer = null;
            if (request.CustomerId > 0)
            {
                customer = (await _unitOfWork.Repository<Customer>().FindAsync(c => c.Id == request.CustomerId, ct)).FirstOrDefault();
                if (customer == null) return BadRequest(new { error = "Selected customer not found." });
            }

            var paymentMethod = PaymentMethods.Normalize(request.PaymentMethod);
            var isCard = paymentMethod == PaymentMethods.Card;
            var paymentNote = isCard ? "Paid by card" : "Paid by cash";
            var resolvedNotes = string.IsNullOrWhiteSpace(request.Notes) ? paymentNote : $"{request.Notes} ({paymentNote})";

            var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
            var productMap = (await _unitOfWork.Repository<Product>().FindAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
            if (productIds.Any(id => !productMap.ContainsKey(id)))
            {
                // Used to skip the line and save the rest, so the sale was recorded short of what
                // the customer paid for.
                return BadRequest(new { error = "An item on this ticket is no longer in the catalog. Remove it and try again." });
            }

            // Price every line before writing anything, so a stale ticket can be turned back cleanly.
            var lines = new List<(CartLineRequest Line, Product Product, SaleRecorder.LineQuote Quote, decimal Charged)>();
            foreach (var line in request.Items)
            {
                var product = productMap[line.ProductId];
                var quote = await _saleRecorder.QuoteAsync(_unitOfWork, product, line.Quantity, ct);
                var charged = keepTicketPrices && line.UnitPrice is decimal paid ? paid : quote.SalePrice;
                lines.Add((line, product, quote, charged));
            }

            var stale = lines.Where(l => !keepTicketPrices && l.Line.UnitPrice is decimal shown && shown != l.Quote.SalePrice).ToList();
            if (stale.Count > 0)
            {
                var names = string.Join(", ", stale.Select(l => l.Product.Name));
                return Conflict(new CheckoutPriceChangeDto
                {
                    Error = $"The price changed for {names}. The ticket has been updated; check the new total and charge again.",
                    Prices = lines.Select(l => new CartLinePriceDto { ProductId = l.Product.Id, UnitPrice = l.Quote.SalePrice }).ToList()
                });
            }

            var locationId = await Mercurius.Services.CurrentLocation.GetIdAsync(_unitOfWork, User);
            var sellerIsVatRegistered = (await _unitOfWork.Repository<Location>().FindAsync(l => l.Id == locationId, ct))
                .FirstOrDefault()?.IsVatRegistered ?? true;
            var now = DateTime.UtcNow;
            var cashierId = Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : Guid.Empty;

            var invoice = new Invoice
            {
                SyncId = syncId,
                CustomerId = request.CustomerId > 0 ? request.CustomerId : (int?)null,
                LocationId = locationId,
                StatusId = (int)StatusCollection.InvoiceStatus.Completed,
                InvoiceDate = invoiceDateUtc,
                InvoiceNumber = SaleRecorder.NewInvoiceNumber(now, syncId),
                Notes = resolvedNotes,
                PaymentMethod = paymentMethod,
                PaidAmount = request.AmountReceived,
                CreatedDate = now,
                // Cashier + origin, shown on every mobile terminal's Receipts list (the Blazor app
                // is the web app, so it reports as Web too).
                CreatedBy = cashierId,
                SourceChannel = "Web"
            };
            var soldBy = new SaleRecorder.SoldBy(cashierId, User.Identity?.Name ?? "Unknown", "web");

            var grandTotal = 0m;
            await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                await _unitOfWork.Repository<Invoice>().AddAsync(invoice, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var recorded = new List<(InvoiceItem Item, Product Product)>();
                foreach (var (line, product, quote, charged) in lines)
                {
                    var item = await _saleRecorder.RecordLineAsync(_unitOfWork, invoice, product, line.Quantity, quote, charged, soldBy, ct: ct);
                    recorded.Add((item, product));
                    grandTotal += charged * line.Quantity;
                }
                SaleRecorder.ApplyTax(invoice, recorded, sellerIsVatRegistered);
                await _unitOfWork.Repository<Invoice>().UpdateAsync(invoice, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                await _saleRecorder.RefreshPricesAsync(_unitOfWork, productIds, ct);
                await _unitOfWork.CommitTransactionAsync(ct);
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
