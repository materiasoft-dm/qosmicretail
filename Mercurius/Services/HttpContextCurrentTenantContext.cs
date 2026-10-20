using System.Security.Claims;
using Mercurius.Common.Constants;
using Mercurius.Repo.Repositories;

namespace Mercurius.Services
{
    // Lets work that runs outside an HTTP request (background jobs, startup seeding) or before
    // the request has a tenant claim (sign-in) act on behalf of a specific tenant. The override
    // lasts until the returned handle is disposed, which restores whatever was in effect before;
    // callers that want it for the rest of their DI scope can simply ignore the handle.
    public interface ITenantScopeOverride
    {
        IDisposable UseTenant(int tenantId);
    }

    // Reads the tenant boundary from whichever ClaimsPrincipal authenticated the current
    // request — cookie (web) or JWT (mobile/Blazor API calls) alike, since both populate
    // HttpContext.User the same way. See MULTITENANCY_ARCHITECTURE.md §4.1.
    //
    // Background work has no HttpContext, so without an override it resolves to -1 and sees/writes
    // nothing — which is exactly what used to happen to CSV product imports: the job ran in a new
    // scope with no user, so imported products were stamped with no real tenant and vanished from
    // every list. ProductImportBackgroundService now calls UseTenant(job.TenantId) (captured from
    // the uploader when the job was queued) before touching the database. Registered once per
    // scope and exposed as both ICurrentTenantContext and ITenantScopeOverride (see Program.cs), so
    // the override is seen by MercuriusDbContext's query filter and EfRepository's stamping alike.
    public class HttpContextCurrentTenantContext : ICurrentTenantContext, ITenantScopeOverride
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private int? _override;

        public HttpContextCurrentTenantContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public IDisposable UseTenant(int tenantId)
        {
            var previous = _override;
            _override = tenantId;
            return new Restore(this, previous);
        }

        private sealed class Restore : IDisposable
        {
            private readonly HttpContextCurrentTenantContext _owner;
            private readonly int? _previous;
            private bool _disposed;

            public Restore(HttpContextCurrentTenantContext owner, int? previous)
            {
                _owner = owner;
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner._override = _previous;
            }
        }

        public int TenantId
        {
            get
            {
                if (_override.HasValue) return _override.Value;

                var value = _httpContextAccessor.HttpContext?.User?.FindFirstValue(MercuriusClaimTypes.TenantId);
                // Fails closed: no claim (unauthenticated request, background service, design-time
                // migration) resolves to an id no real Tenant row can ever have, rather than 0/null
                // — a filter bug then shows up as "sees nothing", not "sees everything".
                return int.TryParse(value, out var tenantId) ? tenantId : -1;
            }
        }
    }
}
