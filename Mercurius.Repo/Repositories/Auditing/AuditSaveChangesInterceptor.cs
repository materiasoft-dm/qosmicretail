using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Mercurius.Repo.Repositories.Auditing
{
    // Writes an AuditLog row for every entity inserted, updated, or deleted through EF — so every
    // controller, the sync API, background imports, and seeding are all covered without each one
    // having to remember to log. (Raw SQL and ExecuteUpdate/ExecuteDelete bypass the change tracker
    // and therefore this interceptor; the raw-SQL admin console logs itself explicitly instead.)
    //
    // Two phases, because neither alone has everything:
    //   SavingChanges — the only point where original values still exist (after the save EF
    //                   accepts the new values as the originals), so the diff is captured here.
    //   SavedChanges  — the only point where database-generated keys of new rows are known, so the
    //                   rows are finalized and written here, on the same connection/transaction
    //                   (a separate connection could deadlock against an open SQLite write
    //                   transaction).
    //
    // Scoped: one instance per DbContext/request, which is what makes the pending-list and
    // re-entrancy flag below safe.
    public class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private const int MaxValueLength = 500;
        private const int MaxSummaryFields = 5;

        private static readonly string[] SensitiveNameFragments = { "password", "securitystamp", "concurrencystamp", "token", "secret" };
        private static readonly string[] LabelProperties = { "Name", "ProductCode", "InvoiceNumber", "OrderNumber", "BatchNumber", "Email", "UserName", "Title" };
        private static readonly HashSet<Type> SkippedTypes = new()
        {
            typeof(AuditLog),
            typeof(IdentityUserToken<string>)
        };

        private readonly IAuditContextProvider _contextProvider;
        private readonly ILogger<AuditSaveChangesInterceptor> _logger;
        private readonly List<PendingAudit> _pending = new();
        private bool _writingAuditRows;

        public AuditSaveChangesInterceptor(IAuditContextProvider contextProvider, ILogger<AuditSaveChangesInterceptor> logger)
        {
            _contextProvider = contextProvider;
            _logger = logger;
        }

        // Serialized as {"old":..,"new":..} per changed field.
        private sealed record ChangePair(
            [property: System.Text.Json.Serialization.JsonPropertyName("old")] object? Old,
            [property: System.Text.Json.Serialization.JsonPropertyName("new")] object? New);

        private sealed class PendingAudit
        {
            public EntityEntry Entry = null!;
            public string Action = "";
            public string EntityType = "";
            public string? KeyBeforeSave;
            public string? Label;
            public int? TenantId;
            public Dictionary<string, object?> Details = new();
            public List<string> SummaryParts = new();
        }

        // ---------------------------------------------------------------- phase 1: capture

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Capture(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Capture(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void Capture(DbContext? context)
        {
            if (context == null || _writingAuditRows) return;
            _pending.Clear();

            try
            {
                context.ChangeTracker.DetectChanges();
                foreach (var entry in context.ChangeTracker.Entries())
                {
                    if (SkippedTypes.Contains(entry.Metadata.ClrType)) continue;
                    if (entry.Metadata.IsOwned()) continue;

                    var pending = entry.State switch
                    {
                        EntityState.Added => CaptureAdded(entry),
                        EntityState.Modified => CaptureModified(entry),
                        EntityState.Deleted => CaptureDeleted(entry),
                        _ => null
                    };
                    if (pending != null) _pending.Add(pending);
                }
            }
            catch (Exception ex)
            {
                // Auditing must never be the reason a real save fails.
                _logger.LogError(ex, "Audit: failed to capture changes; this save will not be audited.");
                _pending.Clear();
            }
        }

        private PendingAudit CaptureAdded(EntityEntry entry)
        {
            var pending = NewPending(entry, "Created");
            foreach (var prop in entry.Properties)
            {
                if (prop.Metadata.IsPrimaryKey() && prop.IsTemporary) continue;
                pending.Details[prop.Metadata.Name] = Present(prop.Metadata.Name, prop.CurrentValue);
            }
            return pending;
        }

        private PendingAudit? CaptureModified(EntityEntry entry)
        {
            var changes = new Dictionary<string, object?>();
            bool? isActiveNow = null;
            foreach (var prop in entry.Properties)
            {
                if (!prop.IsModified) continue;
                // The repository's UpdateAsync marks EVERY property modified; only real value
                // changes are worth recording.
                if (Equals(prop.OriginalValue, prop.CurrentValue)) continue;

                var name = prop.Metadata.Name;
                changes[name] = new ChangePair(Present(name, prop.OriginalValue), Present(name, prop.CurrentValue));
                if (name == "IsActive" && prop.CurrentValue is bool b) isActiveNow = b;
            }

            // Nothing actually changed (e.g. a form re-saved unchanged) — not an action worth a row.
            if (changes.Count == 0) return null;

            var action = isActiveNow switch
            {
                false => "Deactivated", // this codebase's "delete" is a soft delete (IsActive=false)
                true => "Reactivated",
                _ => "Updated"
            };

            var pending = NewPending(entry, action);
            pending.Details = changes;
            foreach (var (name, value) in changes.Take(MaxSummaryFields))
            {
                var pair = (ChangePair)value!;
                pending.SummaryParts.Add($"{name}: {Short(pair.Old)} → {Short(pair.New)}");
            }
            if (changes.Count > MaxSummaryFields) pending.SummaryParts.Add($"+{changes.Count - MaxSummaryFields} more");
            return pending;
        }

        private PendingAudit CaptureDeleted(EntityEntry entry)
        {
            var pending = NewPending(entry, "Deleted");
            foreach (var prop in entry.Properties)
            {
                pending.Details[prop.Metadata.Name] = Present(prop.Metadata.Name, prop.OriginalValue);
            }
            return pending;
        }

        private static PendingAudit NewPending(EntityEntry entry, string action)
        {
            var pending = new PendingAudit
            {
                Entry = entry,
                Action = action,
                EntityType = entry.Metadata.ClrType.Name,
                KeyBeforeSave = entry.State == EntityState.Added ? null : KeyOf(entry),
                Label = LabelOf(entry)
            };

            // The row's own tenant beats the request's — background jobs and startup seeding have
            // no signed-in user, but the rows they touch still belong to a tenant.
            var tenantProp = entry.Metadata.FindProperty("TenantId");
            if (tenantProp != null && entry.Property("TenantId").CurrentValue is int tid && tid > 0)
            {
                pending.TenantId = tid;
            }
            return pending;
        }

        // ---------------------------------------------------------------- phase 2: write

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            if (Flush(eventData.Context))
            {
                try
                {
                    _writingAuditRows = true;
                    eventData.Context!.SaveChanges();
                }
                catch (Exception ex)
                {
                    HandleWriteFailure(eventData.Context!, ex);
                }
                finally
                {
                    _writingAuditRows = false;
                }
            }
            return base.SavedChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Flush(eventData.Context))
            {
                try
                {
                    _writingAuditRows = true;
                    await eventData.Context!.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    HandleWriteFailure(eventData.Context!, ex);
                }
                finally
                {
                    _writingAuditRows = false;
                }
            }
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            if (!_writingAuditRows) _pending.Clear();
            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (!_writingAuditRows) _pending.Clear();
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        // Adds the finalized audit rows to the context; returns whether there's anything to save.
        private bool Flush(DbContext? context)
        {
            if (context == null || _writingAuditRows || _pending.Count == 0) return false;

            try
            {
                var info = _contextProvider.GetRequestInfo(context);
                var logs = new List<AuditLog>(_pending.Count);
                foreach (var p in _pending)
                {
                    var key = p.KeyBeforeSave ?? KeyOf(p.Entry);
                    var log = info.NewLog(p.Action);
                    log.TenantId = p.TenantId ?? info.TenantId;
                    log.EntityType = p.EntityType;
                    log.EntityId = key;
                    log.Summary = BuildSummary(p, key);
                    log.Details = JsonSerializer.Serialize(p.Details);
                    logs.Add(log.ClipToColumnLengths());
                }
                context.Set<AuditLog>().AddRange(logs);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit: failed to build audit rows; this save will not be audited.");
                return false;
            }
            finally
            {
                _pending.Clear();
            }
        }

        private void HandleWriteFailure(DbContext context, Exception ex)
        {
            // The business save already committed; don't turn a logging failure into a
            // reported failure of the real operation. Drop the unsaved audit rows so they aren't
            // retried by the caller's next, unrelated SaveChanges.
            _logger.LogError(ex, "Audit: failed to write audit rows.");
            foreach (var entry in context.ChangeTracker.Entries<AuditLog>().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }
        }

        // ---------------------------------------------------------------- helpers

        private static string BuildSummary(PendingAudit p, string? key)
        {
            var subject = $"{p.EntityType}{(key != null ? " #" + key : "")}{(p.Label != null ? $" '{p.Label}'" : "")}";
            var head = $"{p.Action} {subject}";
            return p.SummaryParts.Count == 0 ? head : $"{head}: {string.Join(", ", p.SummaryParts)}";
        }

        private static string? KeyOf(EntityEntry entry)
        {
            var pk = entry.Metadata.FindPrimaryKey();
            if (pk == null) return null;
            var parts = pk.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "?");
            return string.Join(",", parts);
        }

        private static string? LabelOf(EntityEntry entry)
        {
            foreach (var name in LabelProperties)
            {
                if (entry.Metadata.FindProperty(name) == null) continue;
                var value = entry.Property(name).CurrentValue ?? entry.Property(name).OriginalValue;
                if (value is string s && !string.IsNullOrWhiteSpace(s)) return s.Length > 80 ? s.Substring(0, 79) + "…" : s;
            }
            return null;
        }

        private static object? Present(string propertyName, object? value)
        {
            var lower = propertyName.ToLowerInvariant();
            if (SensitiveNameFragments.Any(f => lower.Contains(f))) return value == null ? null : "***";

            return value switch
            {
                null => null,
                byte[] bytes => $"<{bytes.Length} bytes>",
                string s when s.Length > MaxValueLength => s.Substring(0, MaxValueLength) + "…",
                DateTime or DateTimeOffset or Guid or Enum => value.ToString(),
                _ => value
            };
        }

        private static string Short(object? value)
        {
            var s = value?.ToString() ?? "∅";
            return s.Length > 40 ? s.Substring(0, 39) + "…" : s;
        }
    }
}
