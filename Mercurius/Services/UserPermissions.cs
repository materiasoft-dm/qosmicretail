using Mercurius.Common.Constants;
using Mercurius.Repo.IdentityModel;
using Microsoft.AspNetCore.Identity;

namespace Mercurius.Services
{
    /// <summary>
    /// The ModuleRegistry permissions a user's roles grant, read from the database. Mobile JWTs
    /// carry no access.pages claims (so [Authorize(Policy = ...)] can't gate the sync API), and
    /// reading them live means a permission revoked in Roles management applies to devices
    /// straight away instead of when their long-lived token expires.
    /// </summary>
    public class UserPermissions
    {
        private readonly UserManager<MercuriusUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserPermissions(UserManager<MercuriusUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public virtual async Task<HashSet<string>> GetAsync(string? userId)
        {
            var granted = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(userId)) return granted;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return granted;

            foreach (var roleName in await _userManager.GetRolesAsync(user))
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null) continue;
                foreach (var claim in await _roleManager.GetClaimsAsync(role))
                {
                    if (claim.Type == MercuriusClaimTypes.AccessPages) granted.Add(claim.Value);
                }
            }
            return granted;
        }
    }
}
