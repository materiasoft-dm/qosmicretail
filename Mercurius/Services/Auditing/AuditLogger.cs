using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mercurius.Services.Auditing
{
    // Well-known action names for explicit (non-data-change) events.
    public static class AuditActions
    {
        public const string LoginSucceeded = "LoginSucceeded";
        public const string LoginFailed = "LoginFailed";
        public const string LockedOut = "LockedOut";
        public const string Logout = "Logout";
        public const string PasswordChanged = "PasswordChanged";
        public const string PasswordReset = "PasswordReset";
        public const string DataQueryExecuted = "DataQueryExecuted";
        public const string DataQueryFailed = "DataQueryFailed";
        public const string DataQueryRejected = "DataQueryRejected";
        public const string MobileAppDownloaded = "MobileAppDownloaded";
    }

    // Identifies the actor explicitly, for events where HttpContext.User isn't (yet, or any
    // longer) the right person — at sign-in the principal is still anonymous, and a failed sign-in
    // has only the email that was typed.
    public record AuditActor(string? UserId, string? UserName, int? TenantId);

    public interface IAuditLogger
    {
        Task LogAsync(string action, string summary, object? details = null, AuditActor? actor = null,
            string? entityType = null, string? entityId = null, CancellationToken ct = default);
    }

    // Records events that don't change business data by themselves (data changes are captured
    // automatically by AuditSaveChangesInterceptor). Writes through its own short-lived DbContext so
    // it never accidentally commits whatever unrelated changes the caller's context has pending,
    // and never throws: an audit failure must not break sign-in.
    public class AuditLogger : IAuditLogger
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLogger> _logger;

        public AuditLogger(IServiceScopeFactory scopeFactory, IHttpContextAccessor httpContextAccessor, ILogger<AuditLogger> logger)
        {
            _scopeFactory = scopeFactory;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(string action, string summary, object? details = null, AuditActor? actor = null,
            string? entityType = null, string? entityId = null, CancellationToken ct = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MercuriusDbContext>();
                var provider = scope.ServiceProvider.GetRequiredService<HttpAuditContextProvider>();

                var info = provider.GetRequestInfo(db);
                if (actor != null)
                {
                    info.UserId = actor.UserId;
                    info.UserName = actor.UserName;
                    info.TenantId = actor.TenantId ?? info.TenantId;
                    info.LocationId = null;
                    info.LocationName = null;
                    HttpAuditContextProvider.ResolveLocation(db, info, _httpContextAccessor.HttpContext);
                }

                var log = info.NewLog(action);
                log.Summary = summary;
                log.EntityType = entityType;
                log.EntityId = entityId;
                log.Details = details == null ? null : JsonSerializer.Serialize(details);

                db.AuditLogs.Add(log.ClipToColumnLengths());
                // CancellationToken.None: an audit row for something that already happened should
                // still be written even if the client disconnects mid-request.
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit: failed to record {Action}: {Summary}", action, summary);
            }
        }
    }
}
