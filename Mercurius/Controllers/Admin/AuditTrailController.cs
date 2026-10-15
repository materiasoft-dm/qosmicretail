using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercurius.Controllers.Admin
{
    // Read-only viewer for AuditLog rows (see Mercurius.Repo/Models/AuditLog.cs). Gated by its own
    // ModuleRegistry permission so it can be granted/revoked per role from Roles management, like
    // every other page. Always scoped to the viewer's own tenant: AuditLog deliberately isn't
    // ITenantScoped (see its comment), so the filter is applied explicitly here instead.
    [Authorize(Policy = Common.ModuleRegistry.Pages.ADMIN_AUDIT_TRAIL)]
    public class AuditTrailController : BaseController
    {
        private readonly MercuriusDbContext _context;

        public AuditTrailController(IHttpContextAccessor httpContextAccessor, MercuriusDbContext context)
            : base(httpContextAccessor)
        {
            _context = context;
        }

        private int CurrentTenantId =>
            int.TryParse(User.FindFirstValue(MercuriusClaimTypes.TenantId), out var tid) ? tid : -1;

        private IQueryable<AuditLog> TenantLogs()
        {
            var tenantId = CurrentTenantId;
            return _context.AuditLogs.AsNoTracking().Where(a => a.TenantId == tenantId);
        }

        public async Task<IActionResult> Index(CancellationToken ct = default)
        {
            // Filter dropdowns are built from what actually exists for this tenant, so they never
            // offer an option that can only return zero rows.
            var logs = TenantLogs();
            ViewBag.Actions = await logs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct);
            ViewBag.Sources = await logs.Select(a => a.Source).Distinct().OrderBy(a => a).ToListAsync(ct);
            ViewBag.EntityTypes = await logs.Where(a => a.EntityType != null).Select(a => a.EntityType!).Distinct().OrderBy(a => a).ToListAsync(ct);
            ViewBag.Branches = await logs.Where(a => a.LocationName != null).Select(a => a.LocationName!).Distinct().OrderBy(a => a).ToListAsync(ct);
            return View();
        }

        // GET: AuditTrail/DataTable — jQuery DataTables server-side endpoint (same request/response
        // shape as ProductsController.DataTable). fromUtc/toUtc are computed by the page from the
        // viewer's own local-date pickers, so "today" means the viewer's today.
        [HttpGet]
        public async Task<IActionResult> DataTable(
            int draw = 1, int start = 0, int length = 25,
            DateTime? fromUtc = null, DateTime? toUtc = null,
            string? action = null, string? source = null, string? entityType = null, string? branch = null,
            CancellationToken ct = default)
        {
            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var sortDesc = (string?)Request.Query["order[0][dir]"] != "asc";
            var search = ((string?)Request.Query["search[value]"] ?? "").Trim();

            var baseQuery = TenantLogs();
            var recordsTotal = await baseQuery.CountAsync(ct);

            var query = baseQuery;
            if (fromUtc.HasValue) query = query.Where(a => a.TimestampUtc >= fromUtc.Value);
            if (toUtc.HasValue) query = query.Where(a => a.TimestampUtc < toUtc.Value);
            if (!string.IsNullOrEmpty(action)) query = query.Where(a => a.Action == action);
            if (!string.IsNullOrEmpty(source)) query = query.Where(a => a.Source == source);
            if (!string.IsNullOrEmpty(entityType)) query = query.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrEmpty(branch)) query = query.Where(a => a.LocationName == branch);
            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(a =>
                    (a.UserName != null && a.UserName.ToLower().Contains(s)) ||
                    (a.Summary != null && a.Summary.ToLower().Contains(s)) ||
                    (a.EntityId != null && a.EntityId.ToLower() == s) ||
                    (a.IpAddress != null && a.IpAddress.Contains(s)) ||
                    (a.DeviceName != null && a.DeviceName.ToLower().Contains(s)) ||
                    (a.CorrelationId != null && a.CorrelationId.ToLower() == s));
            }

            var recordsFiltered = await query.CountAsync(ct);

            query = sortDesc
                ? query.OrderByDescending(a => a.TimestampUtc).ThenByDescending(a => a.Id)
                : query.OrderBy(a => a.TimestampUtc).ThenBy(a => a.Id);

            var data = await query.Skip(start).Take(length).Select(a => new
            {
                id = a.Id,
                // Explicit "Z" so the browser converts to the viewer's local time.
                timestampUtc = DateTime.SpecifyKind(a.TimestampUtc, DateTimeKind.Utc),
                userName = a.UserName,
                locationName = a.LocationName,
                source = a.Source,
                deviceName = a.DeviceName,
                action = a.Action,
                entityType = a.EntityType,
                entityId = a.EntityId,
                summary = a.Summary,
                ipAddress = a.IpAddress
            }).ToListAsync(ct);

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        // GET: AuditTrail/Details/5 — everything recorded for one row, for the details modal.
        [HttpGet]
        public async Task<IActionResult> Details(long id, CancellationToken ct = default)
        {
            var log = await TenantLogs().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (log == null) return NotFound();

            return Json(new
            {
                log.Id,
                timestampUtc = DateTime.SpecifyKind(log.TimestampUtc, DateTimeKind.Utc),
                log.UserId,
                log.UserName,
                log.LocationId,
                log.LocationName,
                log.Source,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.Summary,
                log.Details,
                log.IpAddress,
                log.UserAgent,
                log.HttpMethod,
                log.RequestPath,
                log.CorrelationId,
                log.DeviceId,
                log.DeviceName,
                log.AppVersion
            });
        }
    }
}
