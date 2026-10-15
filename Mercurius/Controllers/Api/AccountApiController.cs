using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Services;
using Mercurius.Shared.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // The signed-in user's own account: the app-shell profile (tenant, branch, branches), switching
    // branch, and their personal settings (name, phone, password).
    [ApiController]
    [Route("api/account")]
    public class AccountApiController : ControllerBase
    {
        private readonly UserManager<MercuriusUser> _userManager;
        private readonly SignInManager<MercuriusUser> _signInManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly CurrentUserProfileService _profiles;
        private readonly Mercurius.Services.Auditing.IAuditLogger _auditLogger;

        public AccountApiController(UserManager<MercuriusUser> userManager, SignInManager<MercuriusUser> signInManager,
            IUnitOfWork unitOfWork, CurrentUserProfileService profiles, Mercurius.Services.Auditing.IAuditLogger auditLogger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _unitOfWork = unitOfWork;
            _profiles = profiles;
            _auditLogger = auditLogger;
        }

        // No [Authorize]: an anonymous caller gets IsAuthenticated = false rather than a 401.
        [HttpGet("me")]
        public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct = default) =>
            Ok(await _profiles.GetAsync(User, ct));

        // Makes the chosen branch the one this user works in (the old LocationSelector.SetLocation).
        [HttpPost("branch")]
        [Authorize]
        public async Task<IActionResult> SwitchBranch(SwitchBranchRequest request, CancellationToken ct = default)
        {
            // Query<Location>() is tenant-filtered, so another tenant's branch simply isn't found.
            var target = _unitOfWork.Query<Location>().FirstOrDefault(l => l.Id == request.LocationId);
            if (target == null) return Forbid();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var repo = _unitOfWork.Repository<UserCurrentLocation>();
            var existing = (await repo.FindAsync(c => c.UserId == userId, ct)).FirstOrDefault();
            if (existing == null)
            {
                await repo.AddAsync(new UserCurrentLocation { UserId = userId, LocationId = target.Id }, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            else if (existing.LocationId != target.Id)
            {
                existing.LocationId = target.Id;
                await repo.UpdateAsync(existing, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            return Ok(new BranchOptionDto { Id = target.Id, Name = target.Name ?? string.Empty });
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<AccountProfileDto>> GetProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            return Ok(new AccountProfileDto
            {
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber
            });
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile(UpdateAccountProfileRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return BadRequest(new { error = "First and last name are required." });
            }

            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) return BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });

            // The display name travels in the sign-in cookie; reissue it so the change shows now.
            await _signInManager.RefreshSignInAsync(user);
            return Ok();
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });
            }

            // Changing the password rotates the security stamp; reissue this browser's cookie so the
            // user stays signed in here (other sessions are signed out when the stamp is next checked).
            await _signInManager.RefreshSignInAsync(user);
            await _auditLogger.LogAsync(Mercurius.Services.Auditing.AuditActions.PasswordChanged, $"Changed own password: {user.Email}");
            return Ok();
        }
    }
}
