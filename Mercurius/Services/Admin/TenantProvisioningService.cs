using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Mercurius.Services.Admin
{
    public class TenantProvisioningResult
    {
        public bool Succeeded { get; init; }
        public Tenant? Tenant { get; init; }
        // True when the failure is specifically "that admin email is taken".
        public bool EmailTaken { get; init; }
        public List<string> Errors { get; init; } = new();
    }

    /// <summary>
    /// Creates a brand-new, fully isolated tenant: its own Tenant row, first pharmacy (Location)
    /// and admin user — none of it visible to, or overlapping with, any other tenant's data. Shared
    /// by the classic Platform Admin page and its API. Everything is written in one transaction,
    /// so an admin account Identity rejects (e.g. a weak password) no longer leaves an orphaned
    /// tenant and branch behind.
    /// </summary>
    public class TenantProvisioningService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<MercuriusUser> _userManager;

        public TenantProvisioningService(IUnitOfWork unitOfWork, UserManager<MercuriusUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        public async Task<TenantProvisioningResult> CreateAsync(string tenantName, string firstLocationName,
            string? tenantAddress, string? tenantContactNumber, string adminEmail, string adminPassword, CancellationToken ct)
        {
            if (await _userManager.FindByEmailAsync(adminEmail) != null)
            {
                return new TenantProvisioningResult { EmailTaken = true, Errors = { "A user with this email already exists." } };
            }

            await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                var tenant = new Tenant
                {
                    Name = tenantName.Trim(),
                    Address = string.IsNullOrWhiteSpace(tenantAddress) ? null : tenantAddress.Trim(),
                    ContactNumber = string.IsNullOrWhiteSpace(tenantContactNumber) ? null : tenantContactNumber.Trim(),
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
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
                    Name = firstLocationName.Trim(),
                    AddressId = address.Id,
                    ContactInformationId = contactInformation.Id
                };
                await _unitOfWork.Repository<Location>().AddAsync(location, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var adminUser = new MercuriusUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    // Same as the seeded default admin; they can change it under Account settings.
                    FirstName = "Admin",
                    LastName = "User",
                    IsActive = true,
                    TenantId = tenant.Id
                };
                var createResult = await _userManager.CreateAsync(adminUser, adminPassword);
                if (!createResult.Succeeded)
                {
                    await _unitOfWork.RollbackTransactionAsync(ct);
                    return new TenantProvisioningResult { Errors = createResult.Errors.Select(e => e.Description).ToList() };
                }
                await _userManager.AddToRoleAsync(adminUser, "Administrator");

                await _unitOfWork.Repository<UserCurrentLocation>().AddAsync(
                    new UserCurrentLocation { UserId = adminUser.Id, LocationId = location.Id }, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                await _unitOfWork.CommitTransactionAsync(ct);
                return new TenantProvisioningResult { Succeeded = true, Tenant = tenant };
            }
            catch
            {
                try
                {
                    await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    // Already committed or rolled back — keep the original exception.
                }
                throw;
            }
        }
    }
}
