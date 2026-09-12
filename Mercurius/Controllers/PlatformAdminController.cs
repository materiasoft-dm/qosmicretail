using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercurius.Controllers
{
    // Lives outside every tenant's boundary — see MULTITENANCY_ARCHITECTURE.md §3.6/§4.3. Gated
    // on the PlatformAdmin policy (MercuriusUser.IsPlatformAdmin), not any ModuleRegistry page, so
    // no tenant's own Administrator role can ever be granted access here. Deliberately uses
    // IgnoreQueryFilters() throughout — this is the one place in the app meant to see across
    // every tenant at once.
    [Authorize(Policy = "PlatformAdmin")]
    public class PlatformAdminController : Controller
    {
        private readonly MercuriusDbContext _dbContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<MercuriusUser> _userManager;

        public PlatformAdminController(MercuriusDbContext dbContext, IUnitOfWork unitOfWork, UserManager<MercuriusUser> userManager)
        {
            _dbContext = dbContext;
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            var tenants = await _dbContext.Tenants.IgnoreQueryFilters().OrderBy(t => t.Id).ToListAsync(ct);
            return View(tenants);
        }

        public IActionResult Create() => View(new CreateTenantRequest());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTenantRequest request, CancellationToken ct = default)
        {
            if (!ModelState.IsValid) return View(request);

            var existing = await _userManager.FindByEmailAsync(request.AdminEmail);
            if (existing != null)
            {
                ModelState.AddModelError(nameof(request.AdminEmail), "A user with this email already exists.");
                return View(request);
            }

            // A brand-new, fully isolated tenant: its own Tenant row, its own first pharmacy
            // (Location), and its own admin user — none of it visible to, or overlapping with,
            // any other tenant's data. This is the concrete proof the isolation model actually
            // works, not just the schema.
            var tenant = new Tenant { Name = request.TenantName, IsActive = true, CreatedDate = DateTime.UtcNow };
            await _unitOfWork.Repository<Tenant>().AddAsync(tenant, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var address = new Address { TenantId = tenant.Id, IsActive = true };
            await _unitOfWork.Repository<Address>().AddAsync(address, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var contactInformation = new ContactInformation { TenantId = tenant.Id };
            await _unitOfWork.Repository<ContactInformation>().AddAsync(contactInformation, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var location = new Location
            {
                TenantId = tenant.Id,
                Name = request.FirstLocationName,
                AddressId = address.Id,
                ContactInformationId = contactInformation.Id
            };
            await _unitOfWork.Repository<Location>().AddAsync(location, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var adminUser = new MercuriusUser
            {
                UserName = request.AdminEmail,
                Email = request.AdminEmail,
                EmailConfirmed = true,
                FirstName = request.AdminFirstName,
                LastName = request.AdminLastName,
                TenantId = tenant.Id
            };
            var createResult = await _userManager.CreateAsync(adminUser, request.AdminPassword);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return View(request);
            }
            await _userManager.AddToRoleAsync(adminUser, "Administrator");

            await _unitOfWork.Repository<UserCurrentLocation>().AddAsync(
                new UserCurrentLocation { UserId = adminUser.Id, LocationId = location.Id }, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            TempData["Message"] = $"Tenant \"{tenant.Name}\" created with admin {adminUser.Email}.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class CreateTenantRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string TenantName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        public string FirstLocationName { get; set; } = "Main Branch";

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string AdminEmail { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        public string AdminFirstName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        public string AdminLastName { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
        public string AdminPassword { get; set; } = string.Empty;
    }
}
