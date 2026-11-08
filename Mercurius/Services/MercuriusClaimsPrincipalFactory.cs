using Mercurius.Repo.IdentityModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Mercurius.Services
{
    public class MercuriusClaimsPrincipalFactory : UserClaimsPrincipalFactory<MercuriusUser, MercuriusRole>
    {
        private readonly RoleManager<MercuriusRole> _roleManager;
        private readonly ITenantScopeOverride _tenantScope;

        public MercuriusClaimsPrincipalFactory(
            UserManager<MercuriusUser> userManager,
            RoleManager<MercuriusRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            ITenantScopeOverride tenantScope)
            : base(userManager, roleManager, optionsAccessor)
        {
            _roleManager = roleManager;
            _tenantScope = tenantScope;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(MercuriusUser user)
        {
            // Roles are per tenant and filtered by the current tenant, but at sign-in the request
            // has no tenant claim yet (this is what issues it), so read them as the user's tenant.
            using var _ = _tenantScope.UseTenant(user.TenantId);
            var identity = await base.GenerateClaimsAsync(user);

            // The tenant boundary itself — read by ICurrentTenantContext (Mercurius.Repo) to
            // drive MercuriusDbContext's global query filter. See MULTITENANCY_ARCHITECTURE.md.
            identity.AddClaim(new Claim(Mercurius.Common.Constants.MercuriusClaimTypes.TenantId, user.TenantId.ToString()));
            // The name the app shows for this person (top bar, audit summaries).
            var displayName = $"{user.FirstName} {user.LastName}".Trim();
            if (!string.IsNullOrEmpty(displayName))
            {
                identity.AddClaim(new Claim(ClaimTypes.GivenName, displayName));
            }
            if (user.IsPlatformAdmin)
            {
                identity.AddClaim(new Claim(Mercurius.Common.Constants.MercuriusClaimTypes.IsPlatformAdmin, "true"));
            }

            var roles = await UserManager.GetRolesAsync(user);
            foreach (var roleName in roles)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role != null)
                {
                    var roleClaims = await _roleManager.GetClaimsAsync(role);
                    foreach (var claim in roleClaims)
                    {
                        if (!identity.HasClaim(claim.Type, claim.Value))
                        {
                            identity.AddClaim(claim);
                        }
                    }
                }
            }

            return identity;
        }
    }
}
