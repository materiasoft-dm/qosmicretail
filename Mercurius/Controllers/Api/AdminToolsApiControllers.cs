using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Repositories;
using Mercurius.Services;
using Mercurius.Services.Admin;
using Mercurius.Shared.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Mercurius.Controllers.Api
{
    // Raw SQL console for the Blazor client — same key check, logging and auditing as the classic
    // /DataQuery/Execute (both go through DataQueryService). No antiforgery token here: the body
    // must be JSON, which a cross-site form can't send without a CORS preflight this app never
    // allows, and the per-request key is still required.
    [ApiController]
    [Route("api/admin/data-query")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADMIN_DATA_QUERY)]
    public class DataQueryApiController : ControllerBase
    {
        private readonly DataQueryService _dataQuery;

        public DataQueryApiController(DataQueryService dataQuery)
        {
            _dataQuery = dataQuery;
        }

        [HttpPost]
        public async Task<ActionResult<DataQueryResultDto>> Execute([FromBody] DataQueryRequest request, CancellationToken ct = default)
        {
            var result = await _dataQuery.ExecuteAsync(request.ApiKey, request.Sql, User, ct);
            return result.Outcome switch
            {
                DataQueryOutcome.Succeeded => Ok(new DataQueryResultDto { Columns = result.Columns, Rows = result.Rows, RecordsAffected = result.RecordsAffected }),
                DataQueryOutcome.InvalidKey => Unauthorized(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }
    }

    // The daily SQLite snapshots. Under SQL Server there are none (the hosting provider backs up).
    [ApiController]
    [Route("api/admin/backups")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CONFIG_DATABASE_BACKUPS)]
    public class DatabaseBackupsApiController : ControllerBase
    {
        private readonly DatabaseBackupService _backupService;
        private readonly bool _isSqliteProvider;

        public DatabaseBackupsApiController(DatabaseBackupService backupService, IConfiguration configuration)
        {
            _backupService = backupService;
            _isSqliteProvider = !string.Equals(configuration.GetValue<string>("DatabaseProvider"), "SqlServer", StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet]
        public ActionResult<DatabaseBackupsDto> Get()
        {
            if (!_isSqliteProvider) return Ok(new DatabaseBackupsDto { Enabled = false });

            var backups = new List<DatabaseBackupDto>();
            var directory = _backupService.BackupDirectory;
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory, "*.sqlite").OrderByDescending(f => f))
                {
                    var fi = new FileInfo(file);
                    backups.Add(new DatabaseBackupDto { FileName = fi.Name, CreatedUtc = fi.CreationTimeUtc, SizeBytes = fi.Length });
                }
            }
            return Ok(new DatabaseBackupsDto { Enabled = true, Backups = backups });
        }

        [HttpGet("{fileName}")]
        public IActionResult Download(string fileName)
        {
            if (!_isSqliteProvider) return NotFound();

            // Only files directly inside the backup folder — never a path that escapes it.
            var backupDir = Path.GetFullPath(_backupService.BackupDirectory);
            var path = Path.GetFullPath(Path.Combine(backupDir, fileName));
            if (!path.StartsWith(backupDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !System.IO.File.Exists(path))
            {
                return NotFound();
            }
            return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
        }
    }

    // Tenants — outside every tenant's boundary, so gated on the PlatformAdmin policy
    // (MercuriusUser.IsPlatformAdmin), never on a ModuleRegistry page a tenant's own Administrator
    // role could be granted. Deliberately ignores the tenant query filter.
    [ApiController]
    [Route("api/platform/tenants")]
    [Authorize(Policy = "PlatformAdmin")]
    public class PlatformAdminApiController : ControllerBase
    {
        private readonly MercuriusDbContext _dbContext;
        private readonly TenantProvisioningService _provisioning;

        public PlatformAdminApiController(MercuriusDbContext dbContext, TenantProvisioningService provisioning)
        {
            _dbContext = dbContext;
            _provisioning = provisioning;
        }

        [HttpGet]
        public async Task<ActionResult<List<TenantDto>>> Get(CancellationToken ct = default)
        {
            var tenants = await _dbContext.Tenants.IgnoreQueryFilters().OrderBy(t => t.Id)
                .Select(t => new TenantDto { Id = t.Id, Name = t.Name, IsActive = t.IsActive, CreatedDate = t.CreatedDate })
                .ToListAsync(ct);
            return Ok(tenants);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateTenantRequestDto request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.TenantName) || string.IsNullOrWhiteSpace(request.FirstLocationName)
                || string.IsNullOrWhiteSpace(request.AdminFirstName) || string.IsNullOrWhiteSpace(request.AdminLastName)
                || string.IsNullOrWhiteSpace(request.AdminPassword))
            {
                return BadRequest(new { error = "All fields are required." });
            }
            if (!MailAddress.TryCreate(request.AdminEmail?.Trim(), out _))
            {
                return BadRequest(new { error = "Enter a valid admin email address." });
            }

            var result = await _provisioning.CreateAsync(request.TenantName, request.FirstLocationName,
                request.AdminEmail!.Trim(), request.AdminFirstName.Trim(), request.AdminLastName.Trim(), request.AdminPassword, ct);
            if (!result.Succeeded)
            {
                return BadRequest(new { error = string.Join(" ", result.Errors) });
            }
            return Ok(new TenantDto { Id = result.Tenant!.Id, Name = result.Tenant.Name, IsActive = result.Tenant.IsActive, CreatedDate = result.Tenant.CreatedDate });
        }
    }
}
