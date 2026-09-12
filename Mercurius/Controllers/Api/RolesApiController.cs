using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Shared.Admin;
using Mercurius.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // JSON equivalent of UserManagement/RolesManagerController — the whitelist validation
    // (every submitted page string must be one of ModuleRegistry.Modules) is preserved exactly,
    // since it's what stops arbitrary claim injection.
    [ApiController]
    [Route("api/roles")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADMIN_ROLES_MANAGEMENT)]
    public class RolesApiController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        public RolesApiController(RoleManager<IdentityRole> roleManager) => _roleManager = roleManager;

        [HttpGet]
        public ActionResult<ListResultDto<RoleDto>> Get()
        {
            var items = _roleManager.Roles.OrderBy(r => r.Name)
                .Select(r => new RoleDto { Id = r.Id, Name = r.Name ?? string.Empty }).ToList();
            return Ok(new ListResultDto<RoleDto> { Items = items, Total = items.Count });
        }

        [HttpGet("modules")]
        public ActionResult<List<string>> GetModules() => Ok(Mercurius.Common.ModuleRegistry.Modules.ToList());

        [HttpGet("{id}")]
        public async Task<ActionResult<RoleDetailDto>> GetDetail(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();
            var claims = await _roleManager.GetClaimsAsync(role);
            var selected = claims.Where(c => c.Type == MercuriusClaimTypes.AccessPages).Select(c => c.Value).ToList();
            return Ok(new RoleDetailDto { Id = role.Id, Name = role.Name ?? string.Empty, SelectedPages = selected });
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Role name is required." });
            var role = new IdentityRole(request.Name);
            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded) return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

            var allowed = new HashSet<string>(Mercurius.Common.ModuleRegistry.Modules);
            foreach (var page in request.Pages.Where(p => allowed.Contains(p)))
            {
                await _roleManager.AddClaimAsync(role, new Claim(MercuriusClaimTypes.AccessPages, page));
            }
            return Ok(new { id = role.Id });
        }

        [HttpPut("{id}/pages")]
        public async Task<IActionResult> UpdatePages(string id, UpdateRolePagesRequest request)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();

            var existingClaims = await _roleManager.GetClaimsAsync(role);
            foreach (var claim in existingClaims.Where(c => c.Type == MercuriusClaimTypes.AccessPages))
            {
                await _roleManager.RemoveClaimAsync(role, claim);
            }

            var allowed = new HashSet<string>(Mercurius.Common.ModuleRegistry.Modules);
            foreach (var page in request.Pages.Where(p => allowed.Contains(p)))
            {
                await _roleManager.AddClaimAsync(role, new Claim(MercuriusClaimTypes.AccessPages, page));
            }
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null) return NotFound();
            await _roleManager.DeleteAsync(role);
            return Ok();
        }
    }
}
