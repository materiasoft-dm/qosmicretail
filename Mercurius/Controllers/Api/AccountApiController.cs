using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // Backs the Blazor WASM client's AuthenticationStateProvider. Deliberately reads the
    // existing cookie-authenticated ClaimsPrincipal rather than issuing a separate token — the
    // client is hosted from this same origin, so the browser already sends the Identity cookie
    // on every request, same as any MVC page. No [Authorize] here: an anonymous caller still gets
    // a (IsAuthenticated = false) response rather than a 401, which is what
    // AuthenticationStateProvider expects to build an "anonymous" state from.
    [ApiController]
    [Route("api/account")]
    public class AccountApiController : ControllerBase
    {
        private readonly UserManager<MercuriusUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;

        public AccountApiController(UserManager<MercuriusUser> userManager, IUnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _unitOfWork = unitOfWork;
        }

        [HttpGet("me")]
        public async Task<ActionResult<CurrentUserDto>> Me()
        {
            if (User.Identity is not { IsAuthenticated: true })
            {
                return Ok(new CurrentUserDto { IsAuthenticated = false });
            }

            var tenantIdClaim = User.FindFirstValue(MercuriusClaimTypes.TenantId);
            int.TryParse(tenantIdClaim, out var tenantId);

            string? tenantName = null;
            if (tenantId > 0)
            {
                var tenant = await _unitOfWork.Repository<Tenant>().GetByIdAsync(tenantId);
                tenantName = tenant?.Name;
            }

            return Ok(new CurrentUserDto
            {
                IsAuthenticated = true,
                UserName = User.Identity.Name,
                Email = User.FindFirstValue(ClaimTypes.Email),
                FullName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity.Name,
                Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
                Pages = User.FindAll(MercuriusClaimTypes.AccessPages).Select(c => c.Value).ToList(),
                TenantId = tenantId,
                TenantName = tenantName
            });
        }
    }
}
