using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // Customers (patients): paged/searchable list, full record create/edit, and soft delete
    // (IsActive) — deactivating needs the separate "Customer Delete" permission.
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
                    (c.MiddleName != null && c.MiddleName.ToLower().Contains(s)) ||
                    (c.LastName != null && c.LastName.ToLower().Contains(s)) ||
                    (c.ContactNumber != null && c.ContactNumber.ToLower().Contains(s)) ||
                    (c.EmailAddress != null && c.EmailAddress.ToLower().Contains(s)) ||
                    (c.Tinnumber != null && c.Tinnumber.ToLower().Contains(s)));
            }

            var total = query.Count();
            var items = query.OrderByDescending(c => c.IsActive).ThenByDescending(c => c.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new CustomerListItemDto
                {
                    Id = c.Id,
                    FirstName = c.FirstName ?? string.Empty,
                    MiddleName = c.MiddleName,
                    LastName = c.LastName ?? string.Empty,
                    ContactNumber = c.ContactNumber ?? string.Empty,
                    EmailAddress = c.EmailAddress ?? string.Empty,
                    Age = c.Age,
                    Gender = c.Gender,
                    IsActive = c.IsActive,
                    CreatedDate = c.CreatedDate
                }).ToList();

            return Ok(new CustomerListResultDto { Items = items, Total = total });
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CustomerDetailDto>> GetDetail(int id, CancellationToken ct = default)
        {
            var c = await _unitOfWork.Repository<Customer>().GetByIdAsync(id, ct);
            if (c == null) return NotFound();
            return Ok(new CustomerDetailDto
            {
                Id = c.Id,
                FirstName = c.FirstName ?? string.Empty,
                MiddleName = c.MiddleName,
                LastName = c.LastName ?? string.Empty,
                Age = c.Age,
                Gender = c.Gender,
                BloodType = c.BloodType,
                ContactNumber = c.ContactNumber,
                EmailAddress = c.EmailAddress,
                Tinnumber = c.Tinnumber,
                Allergies = c.Allergies,
                MedicalHistory = c.MedicalHistory,
                IsActive = c.IsActive,
                CreatedDate = c.CreatedDate
            });
        }

        [HttpPost]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CUSTOMER_ADD)]
        public async Task<ActionResult<CustomerListItemDto>> Create(SaveCustomerRequest request, CancellationToken ct = default)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(new { error });

            var customer = new Customer { IsActive = true, CreatedDate = DateTime.UtcNow, CreatedBy = CurrentUserId };
            Apply(customer, request);
            customer.IsActive = true;
            await _unitOfWork.Repository<Customer>().AddAsync(customer, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new CustomerListItemDto
            {
                Id = customer.Id,
                FirstName = customer.FirstName,
                MiddleName = customer.MiddleName,
                LastName = customer.LastName,
                ContactNumber = customer.ContactNumber,
                EmailAddress = customer.EmailAddress,
                Age = customer.Age,
                Gender = customer.Gender,
                IsActive = true,
                CreatedDate = customer.CreatedDate
            });
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CUSTOMER_EDIT)]
        public async Task<IActionResult> Update(int id, SaveCustomerRequest request, CancellationToken ct = default)
        {
            var customer = await _unitOfWork.Repository<Customer>().GetByIdAsync(id, ct);
            if (customer == null) return NotFound();

            var error = Validate(request);
            if (error != null) return BadRequest(new { error });

            // Deactivating is this app's "delete" — it has a permission of its own.
            if (customer.IsActive && !request.IsActive
                && !User.HasClaim(MercuriusClaimTypes.AccessPages, Mercurius.Common.ModuleRegistry.Pages.CUSTOMER_DELETE))
            {
                return StatusCode(403, new { error = "You don't have permission to deactivate customers." });
            }

            Apply(customer, request);
            customer.UpdatedDate = DateTime.UtcNow;
            customer.UpdatedBy = CurrentUserId;
            await _unitOfWork.Repository<Customer>().UpdateAsync(customer, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok();
        }

        private static string? Validate(SaveCustomerRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return "First and last name are required.";
            }
            if (request.Age is < 0 or > 150)
            {
                return "Age must be between 0 and 150.";
            }
            return null;
        }

        private static void Apply(Customer customer, SaveCustomerRequest request)
        {
            customer.FirstName = request.FirstName.Trim();
            customer.MiddleName = request.MiddleName?.Trim() ?? string.Empty;
            customer.LastName = request.LastName.Trim();
            customer.Age = request.Age;
            customer.Gender = Blank(request.Gender);
            customer.BloodType = Blank(request.BloodType);
            customer.ContactNumber = request.ContactNumber?.Trim() ?? string.Empty;
            customer.EmailAddress = request.EmailAddress?.Trim() ?? string.Empty;
            customer.Tinnumber = request.Tinnumber?.Trim() ?? string.Empty;
            customer.Allergies = Blank(request.Allergies);
            customer.MedicalHistory = Blank(request.MedicalHistory);
            customer.IsActive = request.IsActive;
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private Guid CurrentUserId =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;
    }
}
