using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.IdentityModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Mercurius.Controllers.Api
{
    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class LoginResponse
    {
        public string Token { get; set; } = "";
        public DateTime ExpiresAtUtc { get; set; }
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
    }

    // Issues JWTs for the mobile app to authenticate against Api/SyncController — separate from
    // the cookie-based auth the MVC site uses (see Program.cs's JWT bearer registration).
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<MercuriusUser> _userManager;
        private readonly SignInManager<MercuriusUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly Mercurius.Services.Auditing.IAuditLogger _auditLogger;

        public AuthController(UserManager<MercuriusUser> userManager, SignInManager<MercuriusUser> signInManager,
            IConfiguration configuration, Mercurius.Services.Auditing.IAuditLogger auditLogger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _auditLogger = auditLogger;
        }

        // Mobile sign-out is local (the app just discards its token), so this exists purely to
        // put the sign-out on the audit trail. The app calls it best-effort — an offline sign-out
        // simply won't appear.
        [HttpPost("logout")]
        [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Logout(CancellationToken ct = default)
        {
            await _auditLogger.LogAsync(Mercurius.Services.Auditing.AuditActions.Logout,
                $"Signed out of the mobile app: {User.FindFirstValue(ClaimTypes.Email)}", ct: ct);
            return NoContent();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Email and password are required." });
            }

            // Through the sign-in manager, like the web form, so the same rules apply: a wrong
            // password counts towards the 5-attempt lockout (Program.cs), a locked-out account is
            // refused, and so is a deactivated one (MercuriusSignInManager). This used to call
            // UserManager.CheckPasswordAsync, which does none of that — unlimited guessing.
            var user = await _userManager.FindByEmailAsync(request.Email);
            var result = user == null
                ? Microsoft.AspNetCore.Identity.SignInResult.Failed
                : await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (user == null || !result.Succeeded)
            {
                var (action, summary, error) = result.IsLockedOut
                    ? (Mercurius.Services.Auditing.AuditActions.LockedOut,
                        $"Mobile sign-in blocked — account locked out: {request.Email}",
                        "Too many failed sign-in attempts. Try again in 15 minutes.")
                    : result.IsNotAllowed
                        ? (Mercurius.Services.Auditing.AuditActions.LoginFailed,
                            $"Mobile sign-in refused — account deactivated: {request.Email}",
                            "This account has been deactivated. Ask an administrator.")
                        : (Mercurius.Services.Auditing.AuditActions.LoginFailed,
                            $"Failed mobile sign-in attempt for {request.Email}",
                            "Invalid credentials.");
                await _auditLogger.LogAsync(action, summary,
                    new { email = request.Email, channel = "MobileApp" },
                    new Mercurius.Services.Auditing.AuditActor(user?.Id, user?.Email ?? request.Email, user?.TenantId),
                    entityType: user != null ? "MercuriusUser" : null, entityId: user?.Id);
                return Unauthorized(new { error });
            }

            await _auditLogger.LogAsync(Mercurius.Services.Auditing.AuditActions.LoginSucceeded,
                $"Signed in to the mobile app as {user.Email}",
                new { email = user.Email, channel = "MobileApp" },
                new Mercurius.Services.Auditing.AuditActor(user.Id, user.Email, user.TenantId),
                entityType: "MercuriusUser", entityId: user.Id);

            var jwtSection = _configuration.GetSection("Jwt");
            var keyBytes = Encoding.UTF8.GetBytes(jwtSection.GetValue<string>("Key") ?? "");
            var days = jwtSection.GetValue<int?>("AccessTokenDays") ?? 30;
            var expires = DateTime.UtcNow.AddDays(days);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Name, user.FullName),
                // The tenant boundary — read by ICurrentTenantContext to drive
                // MercuriusDbContext's global query filter for every sync/API call this token
                // authenticates. See MULTITENANCY_ARCHITECTURE.md.
                new Claim(Mercurius.Common.Constants.MercuriusClaimTypes.TenantId, user.TenantId.ToString()),
                // Checked on every request (Program.cs): a password reset or deactivation changes
                // the stamp, which ends this token instead of leaving it valid until it expires.
                new Claim(Mercurius.Common.Constants.MercuriusClaimTypes.SecurityStamp, await _userManager.GetSecurityStampAsync(user))
            };

            var token = new JwtSecurityToken(
                issuer: jwtSection.GetValue<string>("Issuer"),
                audience: jwtSection.GetValue<string>("Audience"),
                claims: claims,
                expires: expires,
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256));

            return Ok(new LoginResponse
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAtUtc = expires,
                UserId = user.Id,
                Email = user.Email ?? "",
                FullName = user.FullName
            });
        }
    }
}
