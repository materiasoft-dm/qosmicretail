using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

// One row per auditable action: every insert/update/delete that goes through EF (written by
// AuditSaveChangesInterceptor), plus explicit events that don't change business data on their own
// (sign-in, sign-out, failed sign-in, raw SQL console use — written by the web app's AuditLogger).
//
// Deliberately NOT ITenantScoped: a failed sign-in for an unknown email has no tenant, and the
// global query filter would silently hide such rows. TenantId is still recorded whenever it's
// known, and the Audit Trail page filters on it explicitly.
//
// Who/where/when is snapshotted (UserName, LocationName) rather than only referenced by id, so the
// trail still reads correctly after a user is renamed or a branch is deleted.
public class AuditLog
{
    [Key]
    public long Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    public int? TenantId { get; set; }

    [StringLength(450)]
    public string? UserId { get; set; }

    [StringLength(256)]
    public string? UserName { get; set; }

    public int? LocationId { get; set; }

    [StringLength(200)]
    public string? LocationName { get; set; }

    // Web | MobileApp | Api | System — see AuditSources.
    [StringLength(20)]
    public string Source { get; set; } = "";

    // Created | Updated | Deactivated | Reactivated | Deleted | LoginSucceeded | LoginFailed | ...
    [StringLength(50)]
    public string Action { get; set; } = "";

    [StringLength(100)]
    public string? EntityType { get; set; }

    [StringLength(100)]
    public string? EntityId { get; set; }

    // Human-readable one-liner, e.g. "Updated Product #12 'Paracetamol 500mg': Sale price 10 → 12".
    [StringLength(1000)]
    public string? Summary { get; set; }

    // JSON. For data changes: new values (Created), {old,new} per changed field (Updated), or the
    // removed row's values (Deleted). For explicit events: whatever context that event carries.
    // Secrets (password hashes, security stamps, tokens) are always masked.
    public string? Details { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(512)]
    public string? UserAgent { get; set; }

    [StringLength(10)]
    public string? HttpMethod { get; set; }

    [StringLength(512)]
    public string? RequestPath { get; set; }

    // ASP.NET Core's per-request TraceIdentifier — ties together every row one request produced
    // (e.g. one sale → an Invoice row, its InvoiceItems, and the stock updates).
    [StringLength(100)]
    public string? CorrelationId { get; set; }

    // Mobile only — sent by the app on every request (see Mercurius.Mobile's DeviceInfoHeaderHandler).
    [StringLength(100)]
    public string? DeviceId { get; set; }

    [StringLength(200)]
    public string? DeviceName { get; set; }

    [StringLength(50)]
    public string? AppVersion { get; set; }

    // SQL Server rejects an over-length value outright ("String or binary data would be
    // truncated") — and a failed audit write must never be what breaks a real operation. Clip every
    // bounded column to its declared length before saving.
    public AuditLog ClipToColumnLengths()
    {
        UserId = Clip(UserId, 450);
        UserName = Clip(UserName, 256);
        LocationName = Clip(LocationName, 200);
        Source = Clip(Source, 20) ?? "";
        Action = Clip(Action, 50) ?? "";
        EntityType = Clip(EntityType, 100);
        EntityId = Clip(EntityId, 100);
        Summary = Clip(Summary, 1000);
        IpAddress = Clip(IpAddress, 64);
        UserAgent = Clip(UserAgent, 512);
        HttpMethod = Clip(HttpMethod, 10);
        RequestPath = Clip(RequestPath, 512);
        CorrelationId = Clip(CorrelationId, 100);
        DeviceId = Clip(DeviceId, 100);
        DeviceName = Clip(DeviceName, 200);
        AppVersion = Clip(AppVersion, 50);
        return this;
    }

    private static string? Clip(string? value, int max) =>
        value == null || value.Length <= max ? value : value.Substring(0, max - 1) + "…";
}

public static class AuditSources
{
    public const string Web = "Web";
    public const string MobileApp = "MobileApp";
    public const string Api = "Api";
    public const string System = "System";
}
