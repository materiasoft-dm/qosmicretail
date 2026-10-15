using System;
using System.Linq;
using Mercurius.Repo.Models;

namespace Mercurius.Services.Auditing
{
    // The audit trail's filter/search rules, shared by the classic AuditTrail page and its API so
    // both always return the same rows for the same criteria.
    public static class AuditLogQuery
    {
        public static IQueryable<AuditLog> Filter(IQueryable<AuditLog> query,
            DateTime? fromUtc, DateTime? toUtc,
            string? action, string? source, string? entityType, string? branch, string? search)
        {
            fromUtc = AsUtc(fromUtc);
            toUtc = AsUtc(toUtc);
            if (fromUtc.HasValue) query = query.Where(a => a.TimestampUtc >= fromUtc.Value);
            if (toUtc.HasValue) query = query.Where(a => a.TimestampUtc < toUtc.Value);
            if (!string.IsNullOrEmpty(action)) query = query.Where(a => a.Action == action);
            if (!string.IsNullOrEmpty(source)) query = query.Where(a => a.Source == source);
            if (!string.IsNullOrEmpty(entityType)) query = query.Where(a => a.EntityType == entityType);
            if (!string.IsNullOrEmpty(branch)) query = query.Where(a => a.LocationName == branch);

            search = search?.Trim();
            if (!string.IsNullOrEmpty(search))
            {
                // Record and request ids match exactly; free text matches anywhere.
                var s = search.ToLower();
                query = query.Where(a =>
                    (a.UserName != null && a.UserName.ToLower().Contains(s)) ||
                    (a.Summary != null && a.Summary.ToLower().Contains(s)) ||
                    (a.EntityId != null && a.EntityId.ToLower() == s) ||
                    (a.IpAddress != null && a.IpAddress.Contains(s)) ||
                    (a.DeviceName != null && a.DeviceName.ToLower().Contains(s)) ||
                    (a.CorrelationId != null && a.CorrelationId.ToLower() == s));
            }
            return query;
        }

        // TimestampUtc is compared as a raw value, so normalise whatever Kind the caller or model
        // binder produced: a Local value would otherwise be off by the server's UTC offset.
        private static DateTime? AsUtc(DateTime? value) => value switch
        {
            null => null,
            { Kind: DateTimeKind.Local } v => v.ToUniversalTime(),
            { } v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        };

        public static IQueryable<AuditLog> Order(IQueryable<AuditLog> query, bool newestFirst) =>
            newestFirst
                ? query.OrderByDescending(a => a.TimestampUtc).ThenByDescending(a => a.Id)
                : query.OrderBy(a => a.TimestampUtc).ThenBy(a => a.Id);
    }
}
