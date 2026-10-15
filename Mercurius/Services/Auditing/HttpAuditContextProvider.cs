using System.Linq;
using System.Security.Claims;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Mercurius.Services.Auditing
{
    // Fills in who/where/how for audit rows from the current HTTP request. Scoped, and caches its
    // result for the rest of the request — one request can trigger several saves (e.g. a sale saves
    // the invoice, then its lines, then stock), and the branch lookup only needs to happen once.
    public class HttpAuditContextProvider : IAuditContextProvider
    {
        // Sent by Mercurius.Mobile on every request (see its DeviceInfoHeaderHandler).
        public const string DeviceIdHeader = "X-Device-Id";
        public const string DeviceNameHeader = "X-Device-Name";
        public const string DeviceModelHeader = "X-Device-Model";
        public const string AppVersionHeader = "X-App-Version";
        public const string LocationIdHeader = "X-Location-Id";
        public const string ClientHeader = "X-Client";
        public const string MobileClientName = "MercuriusMobile";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private AuditRequestInfo? _cached;

        public HttpAuditContextProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public AuditRequestInfo GetRequestInfo(DbContext db)
        {
            if (_cached != null) return _cached;

            var http = _httpContextAccessor.HttpContext;
            if (http == null)
            {
                // Background jobs (CSV import) and startup seeding.
                return _cached = new AuditRequestInfo { Source = AuditSources.System };
            }

            var user = http.User;
            var info = new AuditRequestInfo
            {
                UserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
                UserName = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name,
                TenantId = int.TryParse(user.FindFirstValue(MercuriusClaimTypes.TenantId), out var tid) ? tid : null,
                Source = SourceOf(http),
                IpAddress = ClientIpOf(http),
                UserAgent = http.Request.Headers.UserAgent.ToString(),
                HttpMethod = http.Request.Method,
                RequestPath = http.Request.Path + http.Request.QueryString,
                CorrelationId = http.TraceIdentifier,
                DeviceId = HeaderOrNull(http, DeviceIdHeader),
                DeviceName = CombineDeviceName(HeaderOrNull(http, DeviceNameHeader), HeaderOrNull(http, DeviceModelHeader)),
                AppVersion = HeaderOrNull(http, AppVersionHeader)
            };

            ResolveLocation(db, info, http);
            return _cached = info;
        }

        // Branch, in order of preference: the clinic a POS device is assigned to (sent as a
        // header by the mobile app — a shared terminal's branch doesn't depend on who's signed in),
        // then the signed-in user's own current branch (the web app's location switcher).
        public static void ResolveLocation(DbContext db, AuditRequestInfo info, HttpContext? http)
        {
            int? locationId = null;
            if (http != null && int.TryParse(HeaderOrNull(http, LocationIdHeader), out var headerLoc) && headerLoc > 0)
            {
                locationId = headerLoc;
            }
            else if (!string.IsNullOrEmpty(info.UserId))
            {
                var uid = info.UserId;
                var fromUser = db.Set<UserCurrentLocation>().AsNoTracking()
                    .Where(u => u.UserId == uid).Select(u => (int?)u.LocationId).FirstOrDefault();
                if (fromUser > 0) locationId = fromUser;
            }

            if (locationId == null) return;
            info.LocationId = locationId;
            // Unfiltered by tenant on purpose: this is only a display-name lookup for an id we
            // already have, and during sign-in the tenant claim isn't on the principal yet.
            var id = locationId.Value;
            info.LocationName = db.Set<Location>().IgnoreQueryFilters().AsNoTracking()
                .Where(l => l.Id == id).Select(l => l.Name).FirstOrDefault();
        }

        private static string SourceOf(HttpContext http)
        {
            if (string.Equals(HeaderOrNull(http, ClientHeader), MobileClientName, System.StringComparison.OrdinalIgnoreCase)
                || http.Request.Path.StartsWithSegments("/api/sync"))
            {
                return AuditSources.MobileApp;
            }
            return http.Request.Path.StartsWithSegments("/api") ? AuditSources.Api : AuditSources.Web;
        }

        private static string? ClientIpOf(HttpContext http)
        {
            // site4now (IIS) sits in front of the app; honor the first X-Forwarded-For hop when
            // present so the log shows the real client rather than the proxy.
            var forwarded = HeaderOrNull(http, "X-Forwarded-For");
            if (!string.IsNullOrEmpty(forwarded)) return forwarded.Split(',')[0].Trim();
            return http.Connection.RemoteIpAddress?.ToString();
        }

        // "Front Counter (Android 15, Google Pixel Tablet)" — the staff-given terminal name plus
        // the hardware, which is what an investigation actually needs to find the device.
        private static string? CombineDeviceName(string? name, string? model) =>
            name == null ? model : model == null ? name : $"{name} ({model})";

        private static string? HeaderOrNull(HttpContext http, string name)
        {
            var value = http.Request.Headers[name].ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
