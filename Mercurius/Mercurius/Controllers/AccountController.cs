using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common;
using Mercurius.Common.Constants;
using Mercurius.Repo.IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AccountController : ControllerBase
    {
        private readonly SignInManager<MercuriusUser> _signInManager;
        private readonly UserManager<MercuriusUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            SignInManager<MercuriusUser> signInManager,
            UserManager<MercuriusUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpPost]
        [Route("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<UserSession>> Login([FromBody] LoginRequest loginRequest, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var user = await _userManager.FindByEmailAsync(loginRequest.Username);
            if (user is null)
            {
                return Unauthorized();
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginRequest.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                return Unauthorized();
            }

            var roleNames = await _userManager.GetRolesAsync(user);

            var access = new HashSet<string>(StringComparer.Ordinal);
            foreach (var roleName in roleNames)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role is null) continue;

                var roleClaims = await _roleManager.GetClaimsAsync(role);
                foreach (var claim in roleClaims)
                {
                    if (claim.Type == MercuriusClaimTypes.AccessPages)
                    {
                        access.Add(claim.Value);
                    }
                }
            }

            var session = new UserSession
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                IsActive = user.IsActive,
                Role = string.Join(", ", roleNames),
                Access = access.ToList()
            };

            return Ok(session);
        }
    }
}
