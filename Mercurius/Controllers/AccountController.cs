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
        private readonly Mercurius.Services.Auditing.IAuditLogger _auditLogger;

        public AccountController(
            SignInManager<MercuriusUser> signInManager,
            UserManager<MercuriusUser> userManager,
            RoleManager<IdentityRole> roleManager,
            Mercurius.Services.Auditing.IAuditLogger auditLogger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _auditLogger = auditLogger;
        }

        private Task AuditAsync(string action, string summary, MercuriusUser? user, string typedEmail) =>
            _auditLogger.LogAsync(action, summary, new { email = typedEmail, channel = "Api" },
                new Mercurius.Services.Auditing.AuditActor(user?.Id, user?.Email ?? typedEmail, user?.TenantId),
                entityType: user != null ? "MercuriusUser" : null, entityId: user?.Id);

        [HttpPost]
        [Route("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<UserSession>> Login([FromBody] LoginRequest loginRequest, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var user = await _userManager.FindByEmailAsync(loginRequest.Username);
            if (user is null)
            {
                await AuditAsync(Mercurius.Services.Auditing.AuditActions.LoginFailed, $"Failed sign-in attempt for {loginRequest.Username}", null, loginRequest.Username);
                return Unauthorized();
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginRequest.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                await AuditAsync(
                    result.IsLockedOut ? Mercurius.Services.Auditing.AuditActions.LockedOut : Mercurius.Services.Auditing.AuditActions.LoginFailed,
                    result.IsLockedOut ? $"Sign-in blocked — account locked out: {loginRequest.Username}" : $"Failed sign-in attempt for {loginRequest.Username}",
                    user, loginRequest.Username);
                return Unauthorized();
            }

            await AuditAsync(Mercurius.Services.Auditing.AuditActions.LoginSucceeded, $"Signed in as {user.Email}", user, loginRequest.Username);

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
