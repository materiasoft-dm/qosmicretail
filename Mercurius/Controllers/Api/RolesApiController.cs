using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Admin;
using Mercurius.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        private readonly RoleManager<MercuriusRole> _roleManager;
        private readonly ICurrentTenantContext _tenant;

        public RolesApiController(RoleManager<MercuriusRole> roleManager, ICurrentTenantContext tenant)
        {
            _roleManager = roleManager;
            _tenant = tenant;
        }

        // Each tenant manages its own roles: RoleManager only sees the caller's tenant's roles plus
        // platform roles (MercuriusDbContext's role filter), so another tenant's role ids 404 here.
        // The database permissions reach every tenant's data, so only a platform admin may grant or
        // remove them on any role, or change or delete the platform Database Administrator role.
        private bool CallerIsPlatformAdmin => User.HasClaim(MercuriusClaimTypes.IsPlatformAdmin, "true");

        private static bool IsDatabaseAdministratorRole(MercuriusRole role) =>
            role.Name == Mercurius.Common.ModuleRegistry.DatabaseAdministratorRole;

        private ObjectResult DatabasePermissionsPlatformAdminOnly() =>
            StatusCode(StatusCodes.Status403Forbidden, new { error = "Only a platform admin can change database permissions or the Database Administrator role." });

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
            if (!CallerIsPlatformAdmin && request.Pages.Any(p => Mercurius.Common.ModuleRegistry.DatabaseModules.Contains(p)))
            {
                return DatabasePermissionsPlatformAdminOnly();
            }
            var role = new MercuriusRole(request.Name) { TenantId = _tenant.TenantId };
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
            if (!CallerIsPlatformAdmin)
            {
                var databaseModules = Mercurius.Common.ModuleRegistry.DatabaseModules;
                var currentDatabasePages = existingClaims
                    .Where(c => c.Type == MercuriusClaimTypes.AccessPages && databaseModules.Contains(c.Value))
                    .Select(c => c.Value).ToHashSet();
                var requestedDatabasePages = request.Pages.Where(p => databaseModules.Contains(p)).ToHashSet();
                if (IsDatabaseAdministratorRole(role) || !currentDatabasePages.SetEquals(requestedDatabasePages))
                {
                    return DatabasePermissionsPlatformAdminOnly();
                }
            }

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
            if (IsDatabaseAdministratorRole(role) && !CallerIsPlatformAdmin) return DatabasePermissionsPlatformAdminOnly();
            await _roleManager.DeleteAsync(role);
            return Ok();
        }
    }
}
