using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Common.Constants;
using Mercurius.Common.MobileModels;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InvoicesController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<MercuriusUser> _userManager;

        public InvoicesController(IUnitOfWork unitOfWork, UserManager<MercuriusUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InvoiceModel>>> GetInvoices(int skip, int take, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (skip < 0)
            {
                skip = 0;
            }

            if (take <= 0)
            {
                take = 10;
            }

            var collection = _unitOfWork.GetCollection<Invoice>();
            var invoices = collection.Query()
                .OrderByDescending(LiteDB.BsonExpression.Create($"$.{nameof(Invoice.InvoiceDate)}"))
                .Skip(skip)
                .Limit(take)
                .ToList();

            await BatchHydrateInvoicesAsync(invoices, ct);
            var locationIds = invoices.Select(invoice => invoice.LocationId).Where(id => id > 0).Distinct().ToHashSet();
            var locations = await _unitOfWork.Repository<Location>().FindAsync(location => locationIds.Contains(location.Id), ct);
            var locationsById = locations.ToDictionary(location => location.Id);

            var res = invoices.Select(c => new InvoiceModel
            {
                CreatedBy = string.Empty,
                CreatedDate = c.CreatedDate,
                CustomerName = $"{c.Customer?.FirstName} {c.Customer?.LastName}".Trim(),
                Id = c.Id,
                InvoiceDate = c.InvoiceDate,
                InvoiceDueDate = c.InvoiceDueDate,
                InvoiceNumber = c.InvoiceNumber,
                Location = locationsById.TryGetValue(c.LocationId, out var location) ? location.Name : string.Empty,
                Notes = c.Notes,
                Status = ((StatusCollection.InvoiceStatus)c.StatusId).ToString(),
                UpdateBy = string.Empty,
                UpdatedDate = c.UpdatedDate,
                ItemCount = c.InvoiceItems.Count,
                CreatedById = c.CreatedBy,
                UpdateById = c.UpdateBy
            }).ToList();

            foreach (var invoice in res)
            {
                invoice.CreatedBy = await GetUserDisplayNameAsync(invoice.CreatedById, ct) ?? string.Empty;
                if (invoice.UpdateById.HasValue)
                {
                    invoice.UpdateBy = await GetUserDisplayNameAsync(invoice.UpdateById.Value, ct) ?? string.Empty;
                }
            }

            return res;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Invoice>> GetInvoice(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var invoice = await GetHydratedInvoiceAsync(id, ct);

            if (invoice == null)
            {
                return NotFound();
            }

            return invoice;
        }

        private async Task<Invoice?> GetHydratedInvoiceAsync(int id, CancellationToken ct = default)
        {
            var invoice = await _unitOfWork.Repository<Invoice>().GetByIdAsync(id, ct);
            if (invoice == null)
            {
                return null;
            }

            await BatchHydrateInvoicesAsync(new[] { invoice }, ct);
            return invoice;
        }

        /// <summary>
        /// Batch-hydrates invoices by loading all related entities (Customers, InvoiceStatuses,
        /// InvoiceItems, Products, ProductCategories) in a constant number of queries regardless
        /// of invoice count. Replaces the old N+1 per-invoice hydration.
        /// </summary>
        private async Task BatchHydrateInvoicesAsync(IReadOnlyList<Invoice> invoices, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (invoices.Count == 0) return;

            // Collect IDs for entities that need loading
            var customerIds = invoices.Where(i => i.Customer == null && i.CustomerId.HasValue)
                .Select(i => i.CustomerId!.Value).Distinct().ToList();
            var statusIds = invoices.Where(i => i.Status == null && i.StatusId > 0)
                .Select(i => i.StatusId).Distinct().ToList();
            var invoiceIdsNeedingItems = invoices
                .Where(i => i.InvoiceItems == null || i.InvoiceItems.Count == 0)
                .Select(i => i.Id).Distinct().ToList();

            // Batch-load Customers (1 query)
            if (customerIds.Count > 0)
            {
                var customers = await _unitOfWork.Repository<Customer>()
                    .FindAsync(c => customerIds.Contains(c.Id), ct);
                var customerById = customers.ToDictionary(c => c.Id);
                foreach (var invoice in invoices)
                {
                    if (invoice.Customer == null && invoice.CustomerId.HasValue)
                        if (customerById.ContainsKey(invoice.CustomerId.Value)) invoice.Customer = customerById[invoice.CustomerId.Value];
                }
            }

            // Batch-load InvoiceStatuses (1 query)
            if (statusIds.Count > 0)
            {
                var allStatuses = await _unitOfWork.Repository<InvoiceStatus>()
                    .FindAsync(s => statusIds.Contains(s.Id), ct);
                var statusById = allStatuses.ToDictionary(s => s.Id);
                foreach (var invoice in invoices)
                {
                    if (invoice.Status == null && invoice.StatusId > 0)
                        if (statusById.ContainsKey(invoice.StatusId)) invoice.Status = statusById[invoice.StatusId];
                }
            }

            // Batch-load InvoiceItems for all invoices (1 query)
            if (invoiceIdsNeedingItems.Count > 0)
            {
                var allItems = await _unitOfWork.Repository<InvoiceItem>()
                    .FindAsync(ii => invoiceIdsNeedingItems.Contains(ii.InvoiceId), ct);
                var itemsByInvoiceId = allItems.GroupBy(ii => ii.InvoiceId)
                    .ToDictionary(g => g.Key, g => g.ToList());
                foreach (var invoice in invoices)
                {
                    if (invoice.InvoiceItems == null || invoice.InvoiceItems.Count == 0)
                        invoice.InvoiceItems = itemsByInvoiceId.TryGetValue(invoice.Id, out var items)
                            ? items
                            : new List<InvoiceItem>();
                }
            }

            // Collect item-level IDs
            var allInvoiceItems = invoices.SelectMany(i => i.InvoiceItems).ToList();
            var productIds = allInvoiceItems.Where(ii => ii.Product == null && ii.ProductId > 0)
                .Select(ii => ii.ProductId).Distinct().ToList();
            var itemStatusIds = allInvoiceItems.Where(ii => ii.Status == null && ii.StatusId > 0)
                .Select(ii => ii.StatusId).Except(statusIds).Distinct().ToList();

            // Batch-load Products (1 query) + ProductCategories (1 query)
            if (productIds.Count > 0)
            {
                var products = await _unitOfWork.Repository<Product>()
                    .FindAsync(p => productIds.Contains(p.Id), ct);
                var productById = products.ToDictionary(p => p.Id);
                foreach (var item in allInvoiceItems)
                {
                    if (item.Product == null && item.ProductId > 0)
                        if (productById.ContainsKey(item.ProductId)) item.Product = productById[item.ProductId];
                }

                // Batch-load ProductCategories for the products we just loaded
                var categoryIds = products
                    .Where(p => p.ProductCategory == null && p.ProductCategoryId != null)
                    .Select(p => p.ProductCategoryId!.Value).Distinct().ToList();
                if (categoryIds.Count > 0)
                {
                    var categories = await _unitOfWork.Repository<ProductCategory>()
                        .FindAsync(pc => categoryIds.Contains(pc.Id), ct);
                    var categoryById = categories.ToDictionary(pc => pc.Id);
                    foreach (var product in products)
                    {
                        if (product.ProductCategory == null && product.ProductCategoryId != null)
                            if (categoryById.ContainsKey(product.ProductCategoryId.Value)) product.ProductCategory = categoryById[product.ProductCategoryId.Value];
                    }
                }
            }

            // Batch-load InvoiceStatuses for items (1 query, skip IDs already loaded above)
            if (itemStatusIds.Count > 0)
            {
                var itemStatuses = await _unitOfWork.Repository<InvoiceStatus>()
                    .FindAsync(s => itemStatusIds.Contains(s.Id), ct);
                var itemStatusById = itemStatuses.ToDictionary(s => s.Id);
                foreach (var item in allInvoiceItems)
                {
                    if (item.Status == null && item.StatusId > 0)
                        if (itemStatusById.ContainsKey(item.StatusId)) item.Status = itemStatusById[item.StatusId];
                }
            }
        }

        private async Task<string?> GetUserDisplayNameAsync(Guid userId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (userId == Guid.Empty)
            {
                return null;
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());
            return user == null
                ? null
                : $"{user.FirstName} {user.LastName}".Trim();
        }
    }
}