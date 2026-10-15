using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Mercurius.Controllers.Admin
{
    /// <summary>
    /// Raw SQL access to the app's own live database, for the operators of this app only.
    /// Gated by BOTH the ADMIN_DATA_QUERY claim (session cookie) AND a separate secret key
    /// ("AdminQueryApiKey" in configuration) supplied per-request — so a compromised session
    /// cookie alone (e.g. via XSS) isn't enough to use it. Every query attempt is logged.
    /// Full read/write: there is no undo. Treat this like a production DB console.
    /// </summary>
    [Authorize(Policy = Common.ModuleRegistry.Pages.ADMIN_DATA_QUERY)]
    public class DataQueryController : BaseController
    {
        private readonly MercuriusDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DataQueryController> _logger;
        private readonly Mercurius.Services.Auditing.IAuditLogger _auditLogger;

        public DataQueryController(
            IHttpContextAccessor httpContextAccessor,
            MercuriusDbContext context,
            IConfiguration configuration,
            ILogger<DataQueryController> logger,
            Mercurius.Services.Auditing.IAuditLogger auditLogger)
            : base(httpContextAccessor)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _auditLogger = auditLogger;
        }

        // Raw SQL bypasses EF's change tracker, so AuditSaveChangesInterceptor never sees what it
        // changed — the statement itself is the audit record.
        private static string Preview(string? sql)
        {
            var oneLine = System.Text.RegularExpressions.Regex.Replace(sql ?? "", @"\s+", " ").Trim();
            return oneLine.Length > 200 ? oneLine.Substring(0, 199) + "…" : oneLine;
        }

        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        public class ExecuteRequest
        {
            public string ApiKey { get; set; } = "";
            public string Sql { get; set; } = "";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Execute([FromBody] ExecuteRequest request, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var configuredKey = _configuration.GetValue<string>("AdminQueryApiKey");
            if (string.IsNullOrEmpty(configuredKey) || !FixedTimeEquals(request.ApiKey ?? "", configuredKey))
            {
                _logger.LogWarning("DataQuery: rejected — invalid API key. User: {User}", User.Identity?.Name);
                await _auditLogger.LogAsync(Mercurius.Services.Auditing.AuditActions.DataQueryRejected,
                    $"Raw SQL console: rejected (invalid key): {Preview(request.Sql)}", new { sql = request.Sql });
                return Unauthorized(new { error = "Invalid API key." });
            }

            if (string.IsNullOrWhiteSpace(request.Sql))
            {
                return BadRequest(new { error = "Sql is required." });
            }

            // Auditable: every executed statement is logged with who ran it, regardless of outcome.
            _logger.LogWarning("DataQuery: executing by {User}: {Sql}", User.Identity?.Name, request.Sql);

            try
            {
                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                using var command = connection.CreateCommand();
                command.CommandText = request.Sql;
                command.CommandTimeout = 30;

                var columns = new List<string>();
                var rows = new List<Dictionary<string, object?>>();
                int recordsAffected;

                using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(reader.GetName(i));
                    }

                    while (await reader.ReadAsync(ct))
                    {
                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var value = reader.GetValue(i);
                            row[columns[i]] = value is DBNull ? null : value;
                        }
                        rows.Add(row);
                    }

                    recordsAffected = reader.RecordsAffected;
                }

                _logger.LogWarning("DataQuery: succeeded for {User}. Rows returned: {RowCount}, RecordsAffected: {RecordsAffected}",
                    User.Identity?.Name, rows.Count, recordsAffected);
                await _auditLogger.LogAsync(Mercurius.Services.Auditing.AuditActions.DataQueryExecuted,
                    $"Raw SQL console ({recordsAffected} row(s) affected, {rows.Count} returned): {Preview(request.Sql)}",
                    new { sql = request.Sql, recordsAffected, rowsReturned = rows.Count });

                return Json(new { columns, rows, recordsAffected });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataQuery: failed for {User}", User.Identity?.Name);
                await _auditLogger.LogAsync(Mercurius.Services.Auditing.AuditActions.DataQueryFailed,
                    $"Raw SQL console failed: {Preview(request.Sql)}", new { sql = request.Sql, error = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            if (aBytes.Length != bBytes.Length)
            {
                // Still compare against something of the expected length so the failure path
                // takes comparable time either way.
                bBytes = aBytes;
            }
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes) && a.Length == b.Length;
        }
    }
}
