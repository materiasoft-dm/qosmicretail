using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Services.Auditing;
using Mercurius.Shared.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercurius.Controllers.Api
{
    // The audit trail for the Blazor client — same data and rules as AuditTrailController. AuditLog
    // isn't ITenantScoped (see its comment), so every query here is filtered to the caller's tenant
    // explicitly.
    [ApiController]
    [Route("api/audit-trail")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADMIN_AUDIT_TRAIL)]
    public class AuditTrailApiController : ControllerBase
    {
        private readonly MercuriusDbContext _context;

        public AuditTrailApiController(MercuriusDbContext context)
        {
            _context = context;
        }

        private IQueryable<AuditLog> TenantLogs()
        {
            var tenantId = int.TryParse(User.FindFirstValue(MercuriusClaimTypes.TenantId), out var tid) ? tid : -1;
            return _context.AuditLogs.AsNoTracking().Where(a => a.TenantId == tenantId);
        }

        [HttpGet("filters")]
        public async Task<ActionResult<AuditTrailFiltersDto>> GetFilters(CancellationToken ct = default)
        {
            var logs = TenantLogs();
            return Ok(new AuditTrailFiltersDto
            {
                Actions = await logs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct),
                Sources = await logs.Select(a => a.Source).Distinct().OrderBy(a => a).ToListAsync(ct),
                EntityTypes = await logs.Where(a => a.EntityType != null).Select(a => a.EntityType!).Distinct().OrderBy(a => a).ToListAsync(ct),
                Branches = await logs.Where(a => a.LocationName != null).Select(a => a.LocationName!).Distinct().OrderBy(a => a).ToListAsync(ct)
            });
        }

        // fromUtc/toUtc are a [from, to) window the client computes from the viewer's local dates,
        // so "today" means the viewer's today.
        [HttpGet]
        public async Task<ActionResult<AuditLogPageDto>> Get(
            int page = 1, int pageSize = 25,
            DateTime? fromUtc = null, DateTime? toUtc = null,
            string? action = null, string? source = null, string? entityType = null, string? branch = null,
            string? search = null, bool oldestFirst = false,
            CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200;

            var query = AuditLogQuery.Filter(TenantLogs(), fromUtc, toUtc, action, source, entityType, branch, search);
            var total = await query.CountAsync(ct);
            var items = await AuditLogQuery.Order(query, newestFirst: !oldestFirst)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new AuditLogListItemDto
                {
                    Id = a.Id,
                    TimestampUtc = DateTime.SpecifyKind(a.TimestampUtc, DateTimeKind.Utc),
                    UserName = a.UserName,
                    LocationName = a.LocationName,
                    Source = a.Source,
                    DeviceName = a.DeviceName,
                    Action = a.Action,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    Summary = a.Summary,
                    IpAddress = a.IpAddress
                })
                .ToListAsync(ct);

            return Ok(new AuditLogPageDto { Items = items, Total = total });
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<AuditLogDetailDto>> GetDetail(long id, CancellationToken ct = default)
        {
            var log = await TenantLogs().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (log == null) return NotFound();

            return Ok(new AuditLogDetailDto
            {
                Id = log.Id,
                TimestampUtc = DateTime.SpecifyKind(log.TimestampUtc, DateTimeKind.Utc),
                UserId = log.UserId,
                UserName = log.UserName,
                LocationId = log.LocationId,
                LocationName = log.LocationName,
                Source = log.Source,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                Summary = log.Summary,
                Details = log.Details,
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                HttpMethod = log.HttpMethod,
                RequestPath = log.RequestPath,
                CorrelationId = log.CorrelationId,
                DeviceId = log.DeviceId,
                DeviceName = log.DeviceName,
                AppVersion = log.AppVersion
            });
        }
    }
}
