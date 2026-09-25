using Microsoft.EntityFrameworkCore;

namespace Mercurius.Repo.Repositories.Auditing
{
    // Who/where/how for the current operation. Lives here (not in the web project) so the EF
    // interceptor can depend on it; the web app supplies the HttpContext-backed implementation,
    // the same split as ICurrentTenantContext.
    public interface IAuditContextProvider
    {
        // db is passed in (rather than injected) so the implementation can look up the user's
        // branch name on the very context that's saving, without a DI cycle through DbContext.
        AuditRequestInfo GetRequestInfo(DbContext db);
    }

    public class AuditRequestInfo
    {
        public int? TenantId { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public int? LocationId { get; set; }
        public string? LocationName { get; set; }
        public string Source { get; set; } = Models.AuditSources.System;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? HttpMethod { get; set; }
        public string? RequestPath { get; set; }
        public string? CorrelationId { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? AppVersion { get; set; }

        public Models.AuditLog NewLog(string action) => new()
        {
            TimestampUtc = System.DateTime.UtcNow,
            TenantId = TenantId,
            UserId = UserId,
            UserName = UserName,
            LocationId = LocationId,
            LocationName = LocationName,
            Source = Source,
            Action = action,
            IpAddress = IpAddress,
            UserAgent = UserAgent,
            HttpMethod = HttpMethod,
            RequestPath = RequestPath,
            CorrelationId = CorrelationId,
            DeviceId = DeviceId,
            DeviceName = DeviceName,
            AppVersion = AppVersion
        };
    }
}
