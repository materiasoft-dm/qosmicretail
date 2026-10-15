using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Repositories;
using Mercurius.Services.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Mercurius.Services.Admin
{
    public enum DataQueryOutcome
    {
        Succeeded,
        InvalidKey,
        MissingSql,
        Failed
    }

    public class DataQueryResult
    {
        public DataQueryOutcome Outcome { get; init; }
        public List<string> Columns { get; init; } = new();
        public List<Dictionary<string, object?>> Rows { get; init; } = new();
        public int RecordsAffected { get; init; }
        public string? Error { get; init; }
    }

    /// <summary>
    /// Raw SQL against the app's own live database, shared by the classic DataQuery console and the
    /// Blazor admin API. Callers must already have enforced the ADMIN_DATA_QUERY policy; this adds
    /// the second factor — the "AdminQueryApiKey" secret supplied per request — so a stolen session
    /// cookie alone (e.g. via XSS) isn't enough. Every attempt is logged and audited, whatever the
    /// outcome. Full read/write: there is no undo.
    /// </summary>
    public class DataQueryService
    {
        private readonly MercuriusDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DataQueryService> _logger;
        private readonly IAuditLogger _auditLogger;

        public DataQueryService(MercuriusDbContext context, IConfiguration configuration,
            ILogger<DataQueryService> logger, IAuditLogger auditLogger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _auditLogger = auditLogger;
        }

        public async Task<DataQueryResult> ExecuteAsync(string? apiKey, string? sql, ClaimsPrincipal user, CancellationToken ct)
        {
            var userName = user.Identity?.Name;
            var configuredKey = _configuration.GetValue<string>("AdminQueryApiKey");
            if (string.IsNullOrEmpty(configuredKey) || !FixedTimeEquals(apiKey ?? "", configuredKey))
            {
                _logger.LogWarning("DataQuery: rejected — invalid API key. User: {User}", userName);
                await _auditLogger.LogAsync(AuditActions.DataQueryRejected,
                    $"Raw SQL console: rejected (invalid key): {Preview(sql)}", new { sql });
                return new DataQueryResult { Outcome = DataQueryOutcome.InvalidKey, Error = "Invalid API key." };
            }

            if (string.IsNullOrWhiteSpace(sql))
            {
                return new DataQueryResult { Outcome = DataQueryOutcome.MissingSql, Error = "Sql is required." };
            }

            // Auditable: every executed statement is logged with who ran it, regardless of outcome.
            _logger.LogWarning("DataQuery: executing by {User}: {Sql}", userName, sql);

            try
            {
                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                using var command = connection.CreateCommand();
                command.CommandText = sql;
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
                    userName, rows.Count, recordsAffected);
                // Raw SQL bypasses EF's change tracker, so AuditSaveChangesInterceptor never sees what
                // it changed — the statement itself is the audit record.
                await _auditLogger.LogAsync(AuditActions.DataQueryExecuted,
                    $"Raw SQL console ({recordsAffected} row(s) affected, {rows.Count} returned): {Preview(sql)}",
                    new { sql, recordsAffected, rowsReturned = rows.Count });

                return new DataQueryResult { Outcome = DataQueryOutcome.Succeeded, Columns = columns, Rows = rows, RecordsAffected = recordsAffected };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataQuery: failed for {User}", userName);
                await _auditLogger.LogAsync(AuditActions.DataQueryFailed,
                    $"Raw SQL console failed: {Preview(sql)}", new { sql, error = ex.Message });
                return new DataQueryResult { Outcome = DataQueryOutcome.Failed, Error = ex.Message };
            }
        }

        private static string Preview(string? sql)
        {
            var oneLine = System.Text.RegularExpressions.Regex.Replace(sql ?? "", @"\s+", " ").Trim();
            return oneLine.Length > 200 ? oneLine.Substring(0, 199) + "…" : oneLine;
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
