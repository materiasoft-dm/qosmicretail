using Mercurius.Repo.IdentityModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Mercurius.Services
{
    public class MercuriusClaimsPrincipalFactory : UserClaimsPrincipalFactory<MercuriusUser, IdentityRole>
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public MercuriusClaimsPrincipalFactory(
            UserManager<MercuriusUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, roleManager, optionsAccessor)
        {
            _roleManager = roleManager;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(MercuriusUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

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
