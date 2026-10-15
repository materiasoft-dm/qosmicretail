using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Account;
using Microsoft.Extensions.Configuration;

namespace Mercurius.Services
{
    /// <summary>
    /// Builds the signed-in user's profile for the app shell: identity, tenant, the branch they
    /// work in and the branches they can switch to. Used by api/account/me and, during server
    /// prerendering, directly (so the first HTML already shows the right branch without a round trip).
    /// </summary>
    public class CurrentUserProfileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly bool _isSqliteProvider;

        public CurrentUserProfileService(IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _isSqliteProvider = !string.Equals(configuration.GetValue<string>("DatabaseProvider"), "SqlServer", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<CurrentUserDto> GetAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            if (user.Identity is not { IsAuthenticated: true })
            {
                return new CurrentUserDto { IsAuthenticated = false };
            }

            int.TryParse(user.FindFirstValue(MercuriusClaimTypes.TenantId), out var tenantId);
            string? tenantName = null;
            if (tenantId > 0)
            {
                tenantName = (await _unitOfWork.Repository<Tenant>().GetByIdAsync(tenantId, ct))?.Name;
            }

            // Same resolution as the old header: the user's saved branch, else the first one.
            var locations = _unitOfWork.Query<Location>().OrderBy(l => l.Name).ToList();
            var currentLocationId = await CurrentLocationIdAsync(user, ct);
            var current = locations.FirstOrDefault(l => l.Id == currentLocationId) ?? locations.FirstOrDefault();

            return new CurrentUserDto
            {
                IsAuthenticated = true,
                UserName = user.Identity.Name,
                Email = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity.Name,
                FullName = user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity.Name,
                Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
                Pages = user.FindAll(MercuriusClaimTypes.AccessPages).Select(c => c.Value).ToList(),
                TenantId = tenantId,
                TenantName = tenantName,
                IsPlatformAdmin = user.HasClaim(MercuriusClaimTypes.IsPlatformAdmin, "true"),
                CurrentLocationId = current?.Id,
                CurrentLocationName = current?.Name,
                // Inactive branches can't be switched to — except the one the user is already in.
                Branches = locations.Where(l => l.IsActive || l.Id == current?.Id)
                    .Select(l => new BranchOptionDto { Id = l.Id, Name = l.Name ?? string.Empty }).ToList(),
                DatabaseBackupsEnabled = _isSqliteProvider
            };
        }

        private async Task<int> CurrentLocationIdAsync(ClaimsPrincipal user, CancellationToken ct)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return 0;
            var matches = await _unitOfWork.Repository<UserCurrentLocation>().FindAsync(c => c.UserId == userId, ct);
            return matches.FirstOrDefault()?.LocationId ?? 0;
        }
    }
}
