using System.Security.Claims;
using Mercurius.Common;
using Mercurius.Common.Constants;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Mercurius.Services
{
    /// <summary>
    /// Creates and maintains each tenant's own roles (MercuriusRole.TenantId) — see
    /// MULTITENANCY_ARCHITECTURE.md §3.4. Startup seeding runs it for every tenant, and
    /// TenantProvisioningService runs it for a new one, so every tenant always has Administrator,
    /// Manager and User, and its Administrator always carries every non-database permission.
    /// </summary>
    public class TenantRoles
    {
        public const string Administrator = "Administrator";
        public static readonly IReadOnlyList<string> FixedRoles = new[] { Administrator, "Manager", "User" };

        private readonly MercuriusDbContext _db;
        private readonly RoleManager<MercuriusRole> _roleManager;
        private readonly ITenantScopeOverride _tenantScope;
        private readonly ILogger<TenantRoles> _logger;

        public TenantRoles(MercuriusDbContext db, RoleManager<MercuriusRole> roleManager,
            ITenantScopeOverride tenantScope, ILogger<TenantRoles> logger)
        {
            _db = db;
            _roleManager = roleManager;
            _tenantScope = tenantScope;
            _logger = logger;
        }

        /// <summary>
        /// The tenant's fixed roles, created if missing. Administrator gains any permission added to
        /// ModuleRegistry since the last startup, and never keeps a database permission (those
        /// reach every tenant's data and belong to the platform Database Administrator role).
        /// </summary>
        public async Task EnsureForTenantAsync(int tenantId)
        {
            using var _ = _tenantScope.UseTenant(tenantId);

            foreach (var name in FixedRoles)
            {
                if (await _roleManager.RoleExistsAsync(name)) continue;
                ThrowIfFailed(await _roleManager.CreateAsync(new MercuriusRole(name) { TenantId = tenantId }), name);
                _logger.LogInformation("Created role {Role} for tenant {TenantId}", name, tenantId);
            }

            var admin = await _roleManager.FindByNameAsync(Administrator);
            if (admin == null) return;

            var databaseModules = ModuleRegistry.DatabaseModules;
            var existing = await _roleManager.GetClaimsAsync(admin);
            foreach (var page in ModuleRegistry.Modules.Except(databaseModules))
            {
                if (!existing.Any(c => c.Type == MercuriusClaimTypes.AccessPages && c.Value == page))
                {
                    await _roleManager.AddClaimAsync(admin, new Claim(MercuriusClaimTypes.AccessPages, page));
                }
            }
            foreach (var claim in existing.Where(c => c.Type == MercuriusClaimTypes.AccessPages && databaseModules.Contains(c.Value)))
            {
                await _roleManager.RemoveClaimAsync(admin, claim);
            }
        }

        /// <summary>
        /// Platform roles (TenantId null), visible from every tenant: Database Administrator, with
        /// exactly the database permissions.
        /// </summary>
        public async Task EnsurePlatformRolesAsync()
        {
            var name = ModuleRegistry.DatabaseAdministratorRole;
            var role = await _db.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Name == name && r.TenantId == null);
            if (role == null)
            {
                role = new MercuriusRole(name);
                ThrowIfFailed(await _roleManager.CreateAsync(role), name);
                _logger.LogInformation("Created platform role {Role}", name);
            }

            var existing = await _roleManager.GetClaimsAsync(role);
            foreach (var page in ModuleRegistry.DatabaseModules)
            {
                if (!existing.Any(c => c.Type == MercuriusClaimTypes.AccessPages && c.Value == page))
                {
                    await _roleManager.AddClaimAsync(role, new Claim(MercuriusClaimTypes.AccessPages, page));
                }
            }
        }

        /// <summary>
        /// One-time upgrade from the old global roles: every role row with no tenant (other than
        /// the platform roles) becomes a role of each tenant that uses it, with the same
        /// permissions, and each user's membership moves to their own tenant's copy. Nobody's
        /// effective permissions change. Fixed roles go to every tenant; a custom role goes to the
        /// tenants whose users hold it (the default tenant if nobody does). The original row is
        /// kept for one of those tenants, preferring the default one, so its id stays valid.
        /// Idempotent: once no tenant-less role is left, it does nothing.
        /// </summary>
        public async Task<int> SplitLegacyRolesAsync(int defaultTenantId)
        {
            var platformRoles = new[] { ModuleRegistry.DatabaseAdministratorRole };
            var legacyRoles = await _db.Roles.IgnoreQueryFilters()
                .Where(r => r.TenantId == null && !platformRoles.Contains(r.Name))
                .ToListAsync();
            if (legacyRoles.Count == 0) return 0;

            var tenantIds = await _db.Tenants.IgnoreQueryFilters().OrderBy(t => t.Id).Select(t => t.Id).ToListAsync();
            if (!tenantIds.Contains(defaultTenantId)) tenantIds.Insert(0, defaultTenantId);
            var userTenants = await _db.Users.Select(u => new { u.Id, u.TenantId }).ToDictionaryAsync(u => u.Id, u => u.TenantId);

            await using var transaction = await _db.Database.BeginTransactionAsync();
            var copies = 0;
            foreach (var role in legacyRoles)
            {
                var claims = await _db.RoleClaims.Where(c => c.RoleId == role.Id).ToListAsync();
                var memberships = await _db.UserRoles.Where(ur => ur.RoleId == role.Id).ToListAsync();
                int TenantOf(IdentityUserRole<string> ur) => userTenants.TryGetValue(ur.UserId, out var t) ? t : defaultTenantId;

                var targets = FixedRoles.Contains(role.Name ?? string.Empty)
                    ? tenantIds.ToList()
                    : memberships.Select(TenantOf).Where(tenantIds.Contains).Distinct().ToList();
                if (targets.Count == 0) targets.Add(defaultTenantId);

                var keeper = targets.Contains(defaultTenantId) ? defaultTenantId : targets[0];
                role.TenantId = keeper;

                foreach (var tenantId in targets.Where(t => t != keeper))
                {
                    var copy = new MercuriusRole
                    {
                        Name = role.Name,
                        NormalizedName = role.NormalizedName,
                        TenantId = tenantId
                    };
                    _db.Roles.Add(copy);
                    foreach (var claim in claims)
                    {
                        _db.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = copy.Id, ClaimType = claim.ClaimType, ClaimValue = claim.ClaimValue });
                    }
                    foreach (var membership in memberships.Where(m => TenantOf(m) == tenantId))
                    {
                        _db.UserRoles.Remove(membership);
                        _db.UserRoles.Add(new IdentityUserRole<string> { UserId = membership.UserId, RoleId = copy.Id });
                    }
                    copies++;
                }
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation("Split {Roles} shared roles into per-tenant roles ({Copies} copies created)", legacyRoles.Count, copies);
            return legacyRoles.Count;
        }

        private static void ThrowIfFailed(IdentityResult result, string roleName)
        {
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create role {roleName}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}
