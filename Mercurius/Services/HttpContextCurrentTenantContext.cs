using System.Security.Claims;
using Mercurius.Common.Constants;
using Mercurius.Repo.Repositories;

namespace Mercurius.Services
{
    // Reads the tenant boundary from whichever ClaimsPrincipal authenticated the current
    // request — cookie (web) or JWT (mobile/Blazor API calls) alike, since both populate
    // HttpContext.User the same way. See MULTITENANCY_ARCHITECTURE.md §4.1.
    public class HttpContextCurrentTenantContext : ICurrentTenantContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextCurrentTenantContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int TenantId
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(MercuriusClaimTypes.TenantId);
                // Fails closed: no claim (unauthenticated request, background service, design-time
                // migration) resolves to an id no real Tenant row can ever have, rather than 0/null
                // — a filter bug then shows up as "sees nothing", not "sees everything".
                return int.TryParse(value, out var tenantId) ? tenantId : -1;
            }
        }
    }
}
