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
using Mercurius.Shared.Wholesale;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // The Wholesale module: orders sold to a customer at each product's wholesale price, kept apart
    // from the POS Sell page. A wholesale order is an ordinary Invoice (SourceChannel "Wholesale",
    // every line IsWholeSale) so it still counts in sales, the dashboard and the Invoices list, and
    // refunds go through the usual refund flow. What differs from a POS sale: a customer is
    // required, lines are priced at Product.WholesalePrice (retail when none is set), and it can be
    // sold on credit — the status says whether it's paid (Completed), partly paid (PartiallyPaid)
    // or unpaid (DeferredPayment), and later payments are recorded against it.
    // Stock moves exactly as for a POS sale (SaleRecorder). Web only for now; the mobile app
    // doesn't sell wholesale (see CLAUDE.md).
    [ApiController]
    [Route("api/wholesale")]
    [Authorize]
    public class WholesaleApiController : ControllerBase
    {
        public const string SourceChannel = "Wholesale";

        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.SaleRecorder _saleRecorder;

        public WholesaleApiController(IUnitOfWork unitOfWork, Mercurius.Services.SaleRecorder saleRecorder)
        {
            _unitOfWork = unitOfWork;
            _saleRecorder = saleRecorder;
        }

        // sortBy: number | date (default) | due.
        [HttpGet]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.WHOLESALE_INDEX)]
        public ActionResult<WholesaleOrderListResultDto> Get(string? search = null, int page = 1, int pageSize = 25,
            string? sortBy = null, bool desc = true, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200;

            var query = _unitOfWork.Query<Invoice>().Where(i => i.SourceChannel == SourceChannel);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                var matchingCustomerIds = _unitOfWork.Query<Customer>()
                    .Where(c => (c.FirstName != null && c.FirstName.ToLower().Contains(s))
                             || (c.LastName != null && c.LastName.ToLower().Contains(s)))
                    .Select(c => c.Id).ToList();
                query = query.Where(i => (i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(s))
                                         || (i.CustomerId.HasValue && matchingCustomerIds.Contains(i.CustomerId.Value)));
            }

            var total = query.Count();
            IOrderedQueryable<Invoice> ordered = (sortBy ?? "date").ToLowerInvariant() switch
            {
                "number" => desc ? query.OrderByDescending(i => i.InvoiceNumber) : query.OrderBy(i => i.InvoiceNumber),
                "due" => desc ? query.OrderByDescending(i => i.InvoiceDueDate) : query.OrderBy(i => i.InvoiceDueDate),
                _ => desc ? query.OrderByDescending(i => i.InvoiceDate) : query.OrderBy(i => i.InvoiceDate)
            };
            var orders = ordered.ThenByDescending(i => i.Id).Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // One query per lookup for the whole page (no N+1 per order).
            var orderIds = orders.Select(o => o.Id).ToList();
            var lines = orderIds.Count == 0 ? new List<InvoiceItem>()
                : _unitOfWork.Query<InvoiceItem>().Where(ii => orderIds.Contains(ii.InvoiceId)).ToList();
            var refundedQty = RefundedQuantities(lines);
            var customerNames = CustomerNames(orders.Where(o => o.CustomerId.HasValue).Select(o => o.CustomerId!.Value));

            var items = orders.Select(o =>
            {
                var orderLines = lines.Where(l => l.InvoiceId == o.Id).ToList();
                var (orderTotal, refunded) = Totals(orderLines, refundedQty);
                return new WholesaleOrderListItemDto
                {
                    Id = o.Id,
                    OrderNumber = o.InvoiceNumber ?? string.Empty,
                    OrderDate = o.InvoiceDate,
                    DueDate = DueDate(o),
                    CustomerName = o.CustomerId is int cid && customerNames.TryGetValue(cid, out var name) ? name : string.Empty,
                    StatusId = o.StatusId,
                    StatusName = StatusName(o.StatusId),
                    Total = orderTotal,
                    PaidAmount = o.PaidAmount,
                    Balance = orderTotal - refunded - o.PaidAmount,
                    HasRefund = o.HasRefund
                };
            }).ToList();

            return Ok(new WholesaleOrderListResultDto { Items = items, Total = total });
        }

        [HttpGet("{id:int}")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.WHOLESALE_VIEW)]
        public async Task<ActionResult<WholesaleOrderDetailDto>> GetDetail(int id, CancellationToken ct = default)
        {
            var order = await FindOrderAsync(id, ct);
            if (order == null) return NotFound();
            return Ok(BuildDetail(order));
        }

        [HttpGet("products")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.WHOLESALE_CREATE)]
        public ActionResult<List<WholesaleProductOptionDto>> GetProducts(string? search = null)
        {
            var query = _unitOfWork.Query<Product>().Where(p => p.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(p => (p.Name != null && p.Name.ToLower().Contains(s)) || (p.ProductCode != null && p.ProductCode.ToLower().Contains(s))
                    || (p.Barcode != null && p.Barcode.ToLower().Contains(s)));
            }
            var items = query.OrderBy(p => p.Name).Take(50)
                .Select(p => new WholesaleProductOptionDto
                {
                    Id = p.Id,
                    Name = p.Name ?? string.Empty,
                    ProductCode = p.ProductCode ?? string.Empty,
                    WholesalePrice = p.WholesalePrice,
                    RetailPrice = p.CurrentSalePrice,
                    Stock = p.CurrentStock
                }).ToList();
            return Ok(items);
        }

        [HttpGet("customers")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.WHOLESALE_CREATE)]
        public ActionResult<List<CustomerOptionDto>> GetCustomers(string? search = null)
        {
            // Inactive customers are kept on record but not offered for new orders.
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

        [HttpPost]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.WHOLESALE_CREATE)]
        public Task<IActionResult> Create(CreateWholesaleOrderRequest request, CancellationToken ct = default) =>
            Mercurius.Services.ConcurrencyRetry.RunAsync(_unitOfWork, () => CreateOrderAsync(request, ct), ct);

        private async Task<IActionResult> CreateOrderAsync(CreateWholesaleOrderRequest request, CancellationToken ct)
        {
            var syncId = request.SyncId is Guid g && g != Guid.Empty ? g : Guid.NewGuid();
            var existing = (await _unitOfWork.Repository<Invoice>().FindAsync(i => i.SyncId == syncId, ct)).FirstOrDefault();
            if (existing != null)
            {
                return Ok(new CreatedWholesaleOrderDto { Id = existing.Id, OrderNumber = existing.InvoiceNumber ?? string.Empty });
            }

            if (request.Lines == null || request.Lines.Count == 0 || request.Lines.Any(l => l.ProductId <= 0 || l.Quantity <= 0))
            {
                return BadRequest(new { error = "Add at least one product, each with a quantity above zero." });
            }
            if (request.AmountPaid < 0)
            {
                return BadRequest(new { error = "The amount paid can't be negative." });
            }

            var customer = request.CustomerId > 0
                ? (await _unitOfWork.Repository<Customer>().FindAsync(c => c.Id == request.CustomerId && c.IsActive, ct)).FirstOrDefault()
                : null;
            if (customer == null) return BadRequest(new { error = "Choose the customer this order is for." });

            var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
            var productMap = (await _unitOfWork.Repository<Product>().FindAsync(p => productIds.Contains(p.Id) && p.IsActive, ct)).ToDictionary(p => p.Id);
            if (productMap.Count != productIds.Count)
            {
                return BadRequest(new { error = "One of the products no longer exists or has been deactivated." });
            }

            // Priced up front so the payment can be checked before anything is written. The price
            // doesn't depend on which batch the line is drawn from (wholesale prices are flat).
            var orderTotal = request.Lines.Sum(l => UnitPrice(productMap[l.ProductId]) * l.Quantity);
            if (request.AmountPaid > orderTotal)
            {
                return BadRequest(new { error = "The amount paid can't be more than the order total." });
            }

            var now = DateTime.UtcNow;
            var order = new Invoice
            {
                SyncId = syncId,
                CustomerId = customer.Id,
                LocationId = await Mercurius.Services.CurrentLocation.GetIdAsync(_unitOfWork, User),
                StatusId = (int)PaymentStatus(request.AmountPaid, orderTotal),
                InvoiceDate = now,
                // WS- rather than POS sales' INV-, so a wholesale order is recognisable anywhere it's listed.
                InvoiceNumber = Mercurius.Services.SaleRecorder.NewInvoiceNumber(now, syncId).Replace("INV-", "WS-"),
                // Sales without a due date store DateTime.MinValue rather than null.
                InvoiceDueDate = request.DueDate?.Date ?? DateTime.MinValue,
                Notes = request.Notes?.Trim() ?? string.Empty,
                PaidAmount = request.AmountPaid,
                CreatedDate = now,
                CreatedBy = CurrentUserId,
                SourceChannel = SourceChannel
            };
            var soldBy = new Mercurius.Services.SaleRecorder.SoldBy(CurrentUserId, User.Identity?.Name ?? "Unknown", "wholesale");

            await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                await _unitOfWork.Repository<Invoice>().AddAsync(order, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                foreach (var line in request.Lines)
                {
                    var product = productMap[line.ProductId];
                    var quote = await _saleRecorder.QuoteAsync(_unitOfWork, product, line.Quantity, ct);
                    var item = await _saleRecorder.RecordLineAsync(_unitOfWork, order, product, line.Quantity, quote, UnitPrice(product), soldBy, ct: ct);
                    item.IsWholeSale = true;
                }
                await _unitOfWork.SaveChangesAsync(ct);
                await _saleRecorder.RefreshPricesAsync(_unitOfWork, productIds, ct);
                await _unitOfWork.CommitTransactionAsync(ct);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                throw;
            }

            return Ok(new CreatedWholesaleOrderDto { Id = order.Id, OrderNumber = order.InvoiceNumber });
        }

        // Adds to what's been paid so far. There's no separate payment ledger — the audit trail
        // records each change to the paid amount.
        [HttpPost("{id:int}/payments")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.WHOLESALE_RECORD_PAYMENT)]
        public Task<ActionResult<WholesaleOrderDetailDto>> RecordPayment(int id, RecordWholesalePaymentRequest request, CancellationToken ct = default) =>
            Mercurius.Services.ConcurrencyRetry.RunAsync(_unitOfWork, () => RecordPaymentAsync(id, request, ct), ct);

        private async Task<ActionResult<WholesaleOrderDetailDto>> RecordPaymentAsync(int id, RecordWholesalePaymentRequest request, CancellationToken ct)
        {
            var order = await FindOrderAsync(id, ct);
            if (order == null) return NotFound();
            if (request.Amount <= 0) return BadRequest(new { error = "Enter an amount above zero." });

            var lines = _unitOfWork.Query<InvoiceItem>().Where(ii => ii.InvoiceId == order.Id).ToList();
            var (orderTotal, refunded) = Totals(lines, RefundedQuantities(lines));
            var due = orderTotal - refunded;
            var balance = due - order.PaidAmount;
            if (balance <= 0) return BadRequest(new { error = "This order is already fully paid." });
            if (request.Amount > balance)
            {
                return BadRequest(new { error = $"That's more than the balance of {balance:N2}." });
            }

            order.PaidAmount += request.Amount;
            // A refunded order stays labelled Refunded; otherwise the label follows the payment.
            if (order.StatusId != (int)StatusCollection.InvoiceStatus.Refunded)
            {
                order.StatusId = (int)PaymentStatus(order.PaidAmount, due);
            }
            order.UpdatedDate = DateTime.UtcNow;
            order.UpdateBy = CurrentUserId;
            await _unitOfWork.Repository<Invoice>().UpdateAsync(order, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(BuildDetail(order));
        }

        private Guid CurrentUserId => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

        private async Task<Invoice?> FindOrderAsync(int id, CancellationToken ct) =>
            (await _unitOfWork.Repository<Invoice>().FindAsync(i => i.Id == id && i.SourceChannel == SourceChannel, ct)).FirstOrDefault();

        private WholesaleOrderDetailDto BuildDetail(Invoice order)
        {
            var lines = _unitOfWork.Query<InvoiceItem>().Where(ii => ii.InvoiceId == order.Id).OrderBy(ii => ii.Id).ToList();
            var refundedQty = RefundedQuantities(lines);
            var (orderTotal, refunded) = Totals(lines, refundedQty);

            var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
            var products = productIds.Count == 0 ? new Dictionary<int, Product>()
                : _unitOfWork.Query<Product>().Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id);

            var customerName = order.CustomerId is int cid && CustomerNames(new[] { cid }).TryGetValue(cid, out var name) ? name : string.Empty;

            // Users aren't tenant-filtered, but this id comes from this tenant's order.
            var createdById = order.CreatedBy.ToString();
            var creator = _unitOfWork.Query<Mercurius.Repo.IdentityModel.MercuriusUser>().Where(u => u.Id == createdById)
                .Select(u => new { u.FirstName, u.LastName, u.Email }).FirstOrDefault();
            var createdBy = creator == null ? string.Empty
                : string.IsNullOrWhiteSpace($"{creator.FirstName} {creator.LastName}".Trim()) ? creator.Email ?? string.Empty
                : $"{creator.FirstName} {creator.LastName}".Trim();

            return new WholesaleOrderDetailDto
            {
                Id = order.Id,
                OrderNumber = order.InvoiceNumber ?? string.Empty,
                OrderDate = order.InvoiceDate,
                DueDate = DueDate(order),
                CustomerName = customerName,
                StatusId = order.StatusId,
                StatusName = StatusName(order.StatusId),
                Notes = string.IsNullOrWhiteSpace(order.Notes) ? null : order.Notes,
                CreatedBy = createdBy,
                Total = orderTotal,
                RefundedAmount = refunded,
                PaidAmount = order.PaidAmount,
                Balance = orderTotal - refunded - order.PaidAmount,
                Lines = lines.Select(l =>
                {
                    products.TryGetValue(l.ProductId, out var product);
                    refundedQty.TryGetValue(l.Id, out var qtyRefunded);
                    return new WholesaleOrderLineDto
                    {
                        ProductName = product?.Name ?? "(deleted product)",
                        ProductCode = product?.ProductCode ?? string.Empty,
                        Quantity = l.Quantity,
                        QuantityRefunded = qtyRefunded,
                        UnitPrice = LineUnitPrice(l)
                    };
                }).ToList()
            };
        }

        private Dictionary<int, decimal> RefundedQuantities(IReadOnlyCollection<InvoiceItem> lines)
        {
            var lineIds = lines.Select(l => l.Id).ToList();
            return lineIds.Count == 0 ? new Dictionary<int, decimal>()
                : _unitOfWork.Query<InvoiceItemRefund>().Where(r => lineIds.Contains(r.InvoiceItemId)).ToList()
                    .GroupBy(r => r.InvoiceItemId).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));
        }

        private Dictionary<int, string> CustomerNames(IEnumerable<int> ids)
        {
            var list = ids.Distinct().ToList();
            return list.Count == 0 ? new Dictionary<int, string>()
                : _unitOfWork.Query<Customer>().Where(c => list.Contains(c.Id))
                    .ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim());
        }

        private static (decimal Total, decimal Refunded) Totals(IEnumerable<InvoiceItem> lines, IReadOnlyDictionary<int, decimal> refundedQty)
        {
            decimal total = 0m, refunded = 0m;
            foreach (var line in lines)
            {
                var unit = LineUnitPrice(line);
                total += unit * line.Quantity;
                if (refundedQty.TryGetValue(line.Id, out var q)) refunded += unit * q;
            }
            return (total, refunded);
        }

        // What a line is charged per unit: the wholesale price, or retail when none is set.
        private static decimal UnitPrice(Product product) => product.WholesalePrice ?? product.CurrentSalePrice ?? 0m;

        // What one unit actually sold for — a custom line total wins, the same rule as the
        // Invoices page and refunds (wholesale orders don't set one today).
        private static decimal LineUnitPrice(InvoiceItem item) =>
            item.CustomTotalPrice.HasValue && item.Quantity != 0 ? item.CustomTotalPrice.Value / item.Quantity : item.SalePrice;

        private static StatusCollection.InvoiceStatus PaymentStatus(decimal paid, decimal due) =>
            paid >= due ? StatusCollection.InvoiceStatus.Completed
            : paid > 0 ? StatusCollection.InvoiceStatus.PartiallyPaid
            : StatusCollection.InvoiceStatus.DeferredPayment;

        private static DateTime? DueDate(Invoice order) => order.InvoiceDueDate.Year > 1 ? order.InvoiceDueDate : null;

        private static string StatusName(int statusId) => Enum.IsDefined(typeof(StatusCollection.InvoiceStatus), statusId)
            ? ((StatusCollection.InvoiceStatus)statusId).ToString() : "Unknown";
    }
}
