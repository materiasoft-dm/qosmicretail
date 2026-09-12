using System.Linq;
using System.Threading.Tasks;
using Mercurius.Repo.IdentityModel;
using Mercurius.Shared.Admin;
using Mercurius.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // JSON equivalent of UserManagement/UsersController — preserves the self-toggle guard on
    // SetActive (a user can never deactivate their own account through this endpoint) and the
    // "replace all roles" semantics on AssignRoles, exactly as the MVC controller.
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADMIN_USERS_MANAGEMENT)]
    public class UsersApiController : ControllerBase
    {
        private readonly UserManager<MercuriusUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersApiController(UserManager<MercuriusUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<ActionResult<ListResultDto<UserListItemDto>>> Get(bool showDeactivated = false)
        {
            var users = _userManager.Users.Where(u => showDeactivated || u.IsActive).OrderBy(u => u.UserName).Take(200).ToList();
            var items = new System.Collections.Generic.List<UserListItemDto>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                items.Add(new UserListItemDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    FirstName = u.FirstName ?? string.Empty,
                    LastName = u.LastName ?? string.Empty,
                    Roles = string.Join(", ", roles),
                    IsActive = u.IsActive
                });
            }
            return Ok(new ListResultDto<UserListItemDto> { Items = items, Total = items.Count });
        }

        [HttpGet("roles")]
        public ActionResult<System.Collections.Generic.List<RoleDto>> GetRoles()
        {
            var items = _roleManager.Roles.OrderBy(r => r.Name).Select(r => new RoleDto { Id = r.Id, Name = r.Name ?? string.Empty }).ToList();
            return Ok(items);
        }

        [HttpPost("{id}/roles")]
        public async Task<IActionResult> AssignRoles(string id, AssignUserRolesRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Count > 0) await _userManager.RemoveFromRolesAsync(user, currentRoles);

            foreach (var roleId in request.RoleIds)
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role?.Name != null) await _userManager.AddToRoleAsync(user, role.Name);
            }
            return Ok();
        }

        [HttpPost("{id}/active")]
        public async Task<IActionResult> SetActive(string id, SetUserActiveRequest request)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == id)
            {
                return BadRequest(new { error = "You cannot change your own active status." });
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsActive = request.Activate;
            await _userManager.UpdateAsync(user);
            return Ok();
        }
    }
}
