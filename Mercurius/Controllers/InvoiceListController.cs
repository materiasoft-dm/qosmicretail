using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    /// <summary>
    /// MVC controller for the /Invoices route.
    /// Separate from the API InvoicesController at /api/Invoices (which feeds the mobile app).
    /// </summary>
    [Authorize]
    [Route("Invoices")]
    public class InvoiceListController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public InvoiceListController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: /Invoices
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below. We pass an empty list rather than load
        // every Invoice on every page hit.
        [HttpGet]
        [Authorize(Policy = Common.ModuleRegistry.Pages.INVOICE_INDEX)]
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View("~/Views/Invoices/Index.cshtml", Enumerable.Empty<Invoice>());
        }

        // GET: /Invoices/DataTable
        // Server-side endpoint for jQuery DataTables. Honors the standard request
        // shape and replies with { draw, recordsTotal, recordsFiltered, data: [...] }.
        // Customer / Location names and Status text are batch-resolved (no N+1).
        [HttpGet("DataTable")]
        [Authorize(Policy = Common.ModuleRegistry.Pages.INVOICE_INDEX)]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 0;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            // Preserve the legacy default sort (newest first) when no client order specified.
            if (!q.ContainsKey("order[0][column]"))
            {
                sortColumnIndex = 2;
                sortDir = "desc";
            }

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var collection = _unitOfWork.Query<Invoice>();

            var recordsTotal = collection.Count();

            var query = collection;
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();

                // Customer name search: look up matching customer IDs first, then filter
                // invoices by either matching InvoiceNumber OR a matching CustomerId.
                var matchingCustomerIds = _unitOfWork.Query<Customer>()
                    .Where(c =>
                        (c.FirstName != null && c.FirstName.ToLower().Contains(s)) ||
                        (c.LastName != null && c.LastName.ToLower().Contains(s)))
                    .Select(c => c.Id)
                    .ToList();

                if (matchingCustomerIds.Count > 0)
                {
                    query = query.Where(i =>
                        (i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(s)) ||
                        (i.CustomerId.HasValue && matchingCustomerIds.Contains(i.CustomerId.Value)));
                }
                else
                {
                    query = query.Where(i =>
                        i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(s));
                }
            }

            var recordsFiltered = query.Count();

            // Column index → Invoice field. Matches the `columns` array in Index.cshtml.
            // 0 = Id, 1 = InvoiceNumber, 2 = InvoiceDate, 3 = InvoiceDueDate,
            // 4 = Customer (sort by CustomerId — sorting by joined name would require
            //               fetching the full customer list), 5 = Status (sort by StatusId,
            //               which is workflow order: Draft→Completed→Finalized→Refunded→
            //               Deleted→DeferredPayment→PartiallyPaid), 6 = PaidAmount.
            bool desc = sortDir == "desc";
            query = (sortColumnIndex, desc) switch
            {
                (0, true) => query.OrderByDescending(i => i.Id),
                (0, false) => query.OrderBy(i => i.Id),
                (1, true) => query.OrderByDescending(i => i.InvoiceNumber),
                (1, false) => query.OrderBy(i => i.InvoiceNumber),
                (3, true) => query.OrderByDescending(i => i.InvoiceDueDate),
                (3, false) => query.OrderBy(i => i.InvoiceDueDate),
                (4, true) => query.OrderByDescending(i => i.CustomerId),
                (4, false) => query.OrderBy(i => i.CustomerId),
                (5, true) => query.OrderByDescending(i => i.StatusId),
                (5, false) => query.OrderBy(i => i.StatusId),
                (6, true) => query.OrderByDescending(i => i.PaidAmount),
                (6, false) => query.OrderBy(i => i.PaidAmount),
                (_, true) => query.OrderByDescending(i => i.InvoiceDate),
                (_, false) => query.OrderBy(i => i.InvoiceDate)
            };

            var pageItems = query.Skip(start).Take(length).ToList();

            // Batch-resolve customer names (1 query). Customer lookup gives us full name.
            var customerIds = pageItems
                .Where(i => i.CustomerId.HasValue)
                .Select(i => i.CustomerId!.Value)
                .Distinct()
                .ToList();
            var customerLookup = customerIds.Count == 0
                ? new Dictionary<int, string>()
                : _unitOfWork.Query<Customer>()
                    .Where(c => customerIds.Contains(c.Id))
                    .ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim());

            // Resolve Status from the enum (no DB hit — Status text is purely derived from StatusId).
            // The API controller uses the same StatusCollection.InvoiceStatus enum at line 61 of
            // InvoicesController.cs.
            var data = pageItems.Select(i =>
            {
                string customerName = string.Empty;
                if (i.CustomerId.HasValue)
                {
                    customerLookup.TryGetValue(i.CustomerId.Value, out customerName!);
                }
                var statusName = Enum.IsDefined(typeof(StatusCollection.InvoiceStatus), i.StatusId)
                    ? ((StatusCollection.InvoiceStatus)i.StatusId).ToString()
                    : "Unknown";
                return new
                {
                    id = i.Id,
                    invoiceNumber = i.InvoiceNumber ?? string.Empty,
                    invoiceDate = i.InvoiceDate,
                    invoiceDueDate = i.InvoiceDueDate,
                    customerName = customerName ?? string.Empty,
                    statusId = i.StatusId,
                    statusName,
                    paidAmount = i.PaidAmount,
                    hasRefund = i.HasRefund
                };
            }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }
    }
}
