using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // JSON equivalent of CustomersController.DataTable — see MULTITENANCY_ARCHITECTURE.md's
    // Blazor conversion notes. Deliberately mirrors CustomersController exactly: no IsActive
    // filter (that controller has none either — soft-deleted customers still list today).
    [ApiController]
    [Route("api/customers")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CUSTOMER_LIST)]
    public class CustomersApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomersApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public ActionResult<CustomerListResultDto> Get(string? search = null, int page = 1, int pageSize = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200;

            var query = _unitOfWork.Query<Customer>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(c =>
                    (c.FirstName != null && c.FirstName.ToLower().Contains(s)) ||
                    (c.LastName != null && c.LastName.ToLower().Contains(s)) ||
                    (c.ContactNumber != null && c.ContactNumber.ToLower().Contains(s)) ||
                    (c.EmailAddress != null && c.EmailAddress.ToLower().Contains(s)));
            }

            var total = query.Count();
            var items = query.OrderByDescending(c => c.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new CustomerListItemDto
                {
                    Id = c.Id,
                    FirstName = c.FirstName ?? string.Empty,
                    LastName = c.LastName ?? string.Empty,
                    ContactNumber = c.ContactNumber ?? string.Empty,
                    EmailAddress = c.EmailAddress ?? string.Empty
                }).ToList();

            return Ok(new CustomerListResultDto { Items = items, Total = total });
        }

        [HttpPost]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CUSTOMER_ADD)]
        public async Task<ActionResult<CustomerListItemDto>> Create(CreateCustomerRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName))
            {
                return BadRequest(new { error = "First name is required." });
            }

            var customer = new Customer
            {
                FirstName = request.FirstName,
                MiddleName = request.MiddleName ?? string.Empty,
                LastName = request.LastName ?? string.Empty,
                ContactNumber = request.ContactNumber ?? string.Empty,
                EmailAddress = request.EmailAddress ?? string.Empty,
                IsActive = true,
                CreatedDate = System.DateTime.UtcNow
            };
            await _unitOfWork.Repository<Customer>().AddAsync(customer, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new CustomerListItemDto
            {
                Id = customer.Id,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                ContactNumber = customer.ContactNumber,
                EmailAddress = customer.EmailAddress
            });
        }
    }
}
