using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers
{
    [Authorize]
    public class CustomersController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomersController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: Customers
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below. We pass an empty list rather than load
        // every Customer on every page hit.
        [Authorize(Policy = Common.ModuleRegistry.Pages.CUSTOMER_LIST)]
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<Customer>());
        }

        // GET: Customers/DataTable
        // Server-side endpoint for jQuery DataTables. Honors the standard request
        // shape (draw / start / length / order[0][column] / order[0][dir] / search[value])
        // and replies with { draw, recordsTotal, recordsFiltered, data: [...] }.
        // All filtering/ordering/paging is pushed down to LiteDB so the browser
        // only ever receives one page of rows.
        [HttpGet]
        [Authorize(Policy = Common.ModuleRegistry.Pages.CUSTOMER_LIST)]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 0;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            // Column index → Customer field. Matches the `columns` array in Index.cshtml.
            // 0 = Id, 1 = FirstName, 2 = LastName, 3 = ContactNumber, 4 = EmailAddress,
            // 5 = Actions (not sortable).
            string sortField = sortColumnIndex switch
            {
                0 => nameof(Customer.Id),
                1 => nameof(Customer.FirstName),
                2 => nameof(Customer.LastName),
                3 => nameof(Customer.ContactNumber),
                4 => nameof(Customer.EmailAddress),
                _ => nameof(Customer.Id)
            };
            // Preserve the legacy default sort (newest first by Id desc) when no client order specified.
            if (!q.ContainsKey("order[0][column]"))
            {
                sortColumnIndex = 0;
                sortField = nameof(Customer.Id);
                sortDir = "desc";
            }

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var collection = _unitOfWork.GetCollection<Customer>();

            var recordsTotal = collection.Count();

            var query = collection.Query();
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                // Search across the visible columns.
                query = query.Where(c =>
                    (c.FirstName != null && c.FirstName.ToLower().Contains(s)) ||
                    (c.LastName != null && c.LastName.ToLower().Contains(s)) ||
                    (c.ContactNumber != null && c.ContactNumber.ToLower().Contains(s)) ||
                    (c.EmailAddress != null && c.EmailAddress.ToLower().Contains(s)));
            }

            var recordsFiltered = query.Count();

            var bsonField = LiteDB.BsonExpression.Create($"$.{sortField}");
            query = sortDir == "desc"
                ? query.OrderByDescending(bsonField)
                : query.OrderBy(bsonField);

            var pageItems = query.Skip(start).Limit(length).ToList();

            var data = pageItems.Select(c => new
            {
                id = c.Id,
                firstName = c.FirstName ?? string.Empty,
                lastName = c.LastName ?? string.Empty,
                contactNumber = c.ContactNumber ?? string.Empty,
                emailAddress = c.EmailAddress ?? string.Empty
            }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        [Authorize(Policy = Common.ModuleRegistry.Pages.CUSTOMER_ADD)]
        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        [HttpPost]
        [Authorize(Policy = Common.ModuleRegistry.Pages.CUSTOMER_ADD)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                await _unitOfWork.Repository<Customer>().AddAsync(customer, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return RedirectToAction(nameof(Index));
            }
            return View(customer);
        }
    }
}
