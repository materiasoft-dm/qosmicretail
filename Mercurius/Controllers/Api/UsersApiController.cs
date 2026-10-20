using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Repositories;
using Mercurius.Services.Auditing;
using Mercurius.Shared.Admin;
using Mercurius.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // User management for the signed-in admin's own tenant. MercuriusUser is deliberately not
    // covered by the tenant query filter (sign-in has to find a user before any tenant is known),
    // so every action here filters by TenantId explicitly: another tenant's users are neither
    // listed nor reachable by id (they 404). Roles are replaced wholesale on AssignRoles, and
    // nobody can deactivate their own account.
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADMIN_USERS_MANAGEMENT)]
    public class UsersApiController : ControllerBase
    {
        private readonly UserManager<MercuriusUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ICurrentTenantContext _tenant;
        private readonly IAuditLogger _auditLogger;

        public UsersApiController(UserManager<MercuriusUser> userManager, RoleManager<IdentityRole> roleManager,
            ICurrentTenantContext tenant, IAuditLogger auditLogger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _tenant = tenant;
            _auditLogger = auditLogger;
        }

        // Database Administrator reaches every tenant's data, so a tenant's own user managers can
        // neither hand it out nor take it away; only a platform admin changes who holds it.
        private const string DatabaseAdministratorRole = Mercurius.Common.ModuleRegistry.DatabaseAdministratorRole;

        private bool CallerIsPlatformAdmin => User.HasClaim(MercuriusClaimTypes.IsPlatformAdmin, "true");

        [HttpGet]
        public async Task<ActionResult<ListResultDto<UserListItemDto>>> Get(bool showDeactivated = false)
        {
            var tenantId = _tenant.TenantId;
            var currentUserId = _userManager.GetUserId(User);
            var roleIdsByName = _roleManager.Roles.Where(r => r.Name != null).ToDictionary(r => r.Name!, r => r.Id);

            var users = _userManager.Users
                .Where(u => u.TenantId == tenantId && (showDeactivated || u.IsActive))
                .OrderBy(u => u.UserName).Take(500).ToList();
            var items = new List<UserListItemDto>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                items.Add(new UserListItemDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    FirstName = u.FirstName ?? string.Empty,
                    LastName = u.LastName ?? string.Empty,
                    Roles = string.Join(", ", roles.OrderBy(r => r)),
                    RoleIds = roles.Where(roleIdsByName.ContainsKey).Select(r => roleIdsByName[r]).ToList(),
                    IsActive = u.IsActive,
                    IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    IsCurrentUser = u.Id == currentUserId
                });
            }
            return Ok(new ListResultDto<UserListItemDto> { Items = items, Total = items.Count });
        }

        [HttpGet("roles")]
        public ActionResult<List<RoleDto>> GetRoles()
        {
            var items = _roleManager.Roles.OrderBy(r => r.Name).Select(r => new RoleDto { Id = r.Id, Name = r.Name ?? string.Empty }).ToList();
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUserRequest request)
        {
            var email = (request.Email ?? string.Empty).Trim();
            if (email.Length == 0 || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return BadRequest(new { error = "Email, first name and last name are required." });
            }
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            {
                return BadRequest(new { error = "Enter a valid email address." });
            }
            if (await _userManager.FindByEmailAsync(email) != null || await _userManager.FindByNameAsync(email) != null)
            {
                return BadRequest(new { error = "An account with that email already exists." });
            }

            var user = new MercuriusUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                IsActive = true,
                TenantId = _tenant.TenantId
            };
            var roleNames = await ResolveRoleNamesAsync(request.RoleIds);
            if (!CallerIsPlatformAdmin && roleNames.Contains(DatabaseAdministratorRole)) return DatabaseRolePlatformAdminOnly();

            var result = await _userManager.CreateAsync(user, request.Password ?? string.Empty);
            if (!result.Succeeded) return BadRequest(new { error = Describe(result) });

            foreach (var roleName in roleNames)
            {
                await _userManager.AddToRoleAsync(user, roleName);
            }
            return Ok(new { id = user.Id });
        }

        [HttpPost("{id}/roles")]
        public async Task<IActionResult> AssignRoles(string id, AssignUserRolesRequest request)
        {
            var user = await FindInTenantAsync(id);
            if (user == null) return NotFound();
            if (user.IsPlatformAdmin && !CallerIsPlatformAdmin) return PlatformAdminOnly();

            var currentRoles = await _userManager.GetRolesAsync(user);
            var roleNames = await ResolveRoleNamesAsync(request.RoleIds);
            if (!CallerIsPlatformAdmin && currentRoles.Contains(DatabaseAdministratorRole) != roleNames.Contains(DatabaseAdministratorRole))
            {
                return DatabaseRolePlatformAdminOnly();
            }

            if (currentRoles.Count > 0) await _userManager.RemoveFromRolesAsync(user, currentRoles);
            foreach (var roleName in roleNames)
            {
                await _userManager.AddToRoleAsync(user, roleName);
            }
            return Ok();
        }

        [HttpPost("{id}/active")]
        public async Task<IActionResult> SetActive(string id, SetUserActiveRequest request)
        {
            if (_userManager.GetUserId(User) == id)
            {
                return BadRequest(new { error = "You cannot change your own active status." });
            }

            var user = await FindInTenantAsync(id);
            if (user == null) return NotFound();
            if (user.IsPlatformAdmin && !CallerIsPlatformAdmin) return PlatformAdminOnly();

            user.IsActive = request.Activate;
            await _userManager.UpdateAsync(user);
            return Ok();
        }

        // Sets a new password for a user who forgot theirs, and clears any sign-in lockout. Their
        // other sessions are signed out (the security stamp changes).
        [HttpPost("{id}/password")]
        public async Task<IActionResult> ResetPassword(string id, ResetUserPasswordRequest request)
        {
            var user = await FindInTenantAsync(id);
            if (user == null) return NotFound();
            if (user.IsPlatformAdmin && !CallerIsPlatformAdmin) return PlatformAdminOnly();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword ?? string.Empty);
            if (!result.Succeeded) return BadRequest(new { error = Describe(result) });

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            await _auditLogger.LogAsync(AuditActions.PasswordReset, $"Password reset for {user.Email}",
                entityType: nameof(MercuriusUser), entityId: user.Id);
            return Ok();
        }

        private async Task<MercuriusUser?> FindInTenantAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            return user != null && user.TenantId == _tenant.TenantId ? user : null;
        }

        private async Task<List<string>> ResolveRoleNamesAsync(IEnumerable<string>? roleIds)
        {
            var names = new List<string>();
            foreach (var roleId in (roleIds ?? Enumerable.Empty<string>()).Distinct())
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role?.Name != null) names.Add(role.Name);
            }
            return names;
        }

        private ObjectResult DatabaseRolePlatformAdminOnly() =>
            StatusCode(StatusCodes.Status403Forbidden, new { error = $"Only a platform admin can grant or remove the {DatabaseAdministratorRole} role." });

        private ObjectResult PlatformAdminOnly() =>
            StatusCode(StatusCodes.Status403Forbidden, new { error = "Only a platform admin can change a platform admin's account." });

        private static string Describe(IdentityResult result) =>
            string.Join(" ", result.Errors.Select(e => e.Description));
    }
}
