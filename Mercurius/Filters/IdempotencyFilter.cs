using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mercurius.Filters
{
    /// <summary>
    /// Makes a change that arrives with an <c>Idempotency-Key</c> header happen once. A device's outbox
    /// sends every queued change with its own key, so if the answer is lost (the connection dropped, the
    /// laptop closed) the resend can't create a second refund, adjustment or customer: the repeat gets
    /// the first request's answer back instead of running the change again.
    ///
    /// - The first request claims the key (a row with StatusCode 0), runs, and stores its answer.
    /// - A repeat while that is still running gets 503 + Retry-After (the outbox tries again later).
    /// - A repeat after it finished gets the stored status and body, with <c>Idempotent-Replayed: true</c>.
    /// - A request that fails with a 5xx or throws releases the key, so a resend runs it for real.
    /// - Answers below 500 are stored, refusals included: the same refusal is the right answer to a resend.
    ///
    /// The key is scoped to the signed-in user (and hashed, so the table holds no identifiers). Requests
    /// without the header, and non-JSON results (files), are untouched. It uses its own database scope:
    /// the controller's unit of work must never be saved or rolled back by this bookkeeping.
    /// </summary>
    public sealed class IdempotencyFilter : IAsyncActionFilter
    {
        public const string HeaderName = "Idempotency-Key";
        public const string ReplayedHeader = "Idempotent-Replayed";
        private static readonly TimeSpan AbandonedAfter = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan KeepFor = TimeSpan.FromDays(30);

        private readonly IServiceScopeFactory _scopes;
        private readonly IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> _json;
        private readonly ILogger<IdempotencyFilter> _logger;

        public IdempotencyFilter(IServiceScopeFactory scopes, IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> json, ILogger<IdempotencyFilter> logger)
        {
            _scopes = scopes;
            _json = json;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            var method = http.Request.Method;
            var mutating = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
            var header = http.Request.Headers[HeaderName].ToString().Trim();
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!mutating || header.Length == 0 || string.IsNullOrEmpty(userId))
            {
                await next();
                return;
            }
            if (header.Length > 80)
            {
                context.Result = new BadRequestObjectResult(new { error = "The Idempotency-Key is too long." });
                return;
            }

            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}|{header}")));
            var ct = http.RequestAborted;

            // Claim the key, or find out what became of the earlier request that did.
            using (var scope = _scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MercuriusDbContext>();
                var existing = await db.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == key, ct);
                if (existing != null && existing.StatusCode == 0 && DateTime.UtcNow - existing.CreatedUtc > AbandonedAfter)
                {
                    // The server stopped while it was running (a deploy, a crash): nothing was recorded, so run it.
                    db.IdempotencyRecords.Remove(existing);
                    await db.SaveChangesAsync(ct);
                    existing = null;
                }
                if (existing != null)
                {
                    context.Result = existing.StatusCode == 0 ? StillRunning(http) : Replay(http, existing);
                    return;
                }

                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Key = key,
                    UserId = userId,
                    TenantId = int.TryParse(http.User.FindFirstValue(MercuriusClaimTypes.TenantId), out var tenantId) ? tenantId : null,
                    Method = method,
                    Path = http.Request.Path.Value?.Length > 400 ? http.Request.Path.Value[..400] : http.Request.Path.Value ?? "",
                    StatusCode = 0,
                    CreatedUtc = DateTime.UtcNow
                });
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Two copies of the same request arrived together and the other one claimed the key first.
                    context.Result = StillRunning(http);
                    return;
                }
            }

            ActionExecutedContext executed;
            try
            {
                executed = await next();
            }
            catch
            {
                await ReleaseAsync(key);
                throw;
            }

            var captured = Capture(executed.Result);
            if (executed.Exception != null && !executed.ExceptionHandled || captured == null || captured.Value.Status >= 500)
            {
                // Failed, or an answer that can't be replayed (a file): let a resend run it for real.
                await ReleaseAsync(key);
                return;
            }
            await CompleteAsync(key, captured.Value);
            MaybeCleanUp();
        }

        private (int Status, string? Body, string? ContentType)? Capture(IActionResult? result)
        {
            switch (result)
            {
                case ObjectResult obj:
                    var status = obj.StatusCode ?? 200;
                    if (obj.Value == null) return (status, null, null);
                    return (status, JsonSerializer.Serialize(obj.Value, obj.Value.GetType(), _json.Value.JsonSerializerOptions), "application/json; charset=utf-8");
                case StatusCodeResult plain:
                    return (plain.StatusCode, null, null);
                case EmptyResult:
                    return (200, null, null);
                default:
                    return null;
            }
        }

        private static IActionResult Replay(HttpContext http, IdempotencyRecord record)
        {
            http.Response.Headers[ReplayedHeader] = "true";
            return new ContentResult { StatusCode = record.StatusCode, Content = record.ResponseBody, ContentType = record.ContentType };
        }

        private static IActionResult StillRunning(HttpContext http)
        {
            http.Response.Headers.RetryAfter = "5";
            return new ObjectResult(new { error = "This change is already being processed. It will be confirmed shortly." }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }

        private async Task CompleteAsync(string key, (int Status, string? Body, string? ContentType) response)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MercuriusDbContext>();
                var record = await db.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == key);
                if (record == null) return;
                record.StatusCode = response.Status;
                record.ResponseBody = response.Body;
                record.ContentType = response.ContentType;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // The change itself succeeded; only its replay record is missing. A resend would run again,
                // so leave the claim in place (it expires) rather than fail a request that worked.
                _logger.LogError(ex, "Could not store the answer for an idempotent request");
            }
        }

        private async Task ReleaseAsync(string key)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MercuriusDbContext>();
                await db.IdempotencyRecords.Where(r => r.Key == key).ExecuteDeleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not release an idempotency key");
            }
        }

        // Old records are dropped now and then, by whichever request happens along: no separate job to run.
        private void MaybeCleanUp()
        {
            if (Random.Shared.Next(200) != 0) return;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MercuriusDbContext>();
                    var cutoff = DateTime.UtcNow - KeepFor;
                    await db.IdempotencyRecords.Where(r => r.CreatedUtc < cutoff).ExecuteDeleteAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Idempotency cleanup failed");
                }
            });
        }
    }
}
