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
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<MercuriusUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Email and password are required." });
            }

            // Note: MercuriusUser.IsActive is not checked here — it's purely a display field in
            // this codebase today (e.g. AccountController's user list), not enforced by the
            // existing cookie-based sign-in either, so gating on it here would be an inconsistent,
            // surprise restriction relative to how the rest of the app already behaves.
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            {
                return Unauthorized(new { error = "Invalid credentials." });
            }

            var jwtSection = _configuration.GetSection("Jwt");
            var keyBytes = Encoding.UTF8.GetBytes(jwtSection.GetValue<string>("Key") ?? "");
            var days = jwtSection.GetValue<int?>("AccessTokenDays") ?? 30;
            var expires = DateTime.UtcNow.AddDays(days);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Name, user.FullName)
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
