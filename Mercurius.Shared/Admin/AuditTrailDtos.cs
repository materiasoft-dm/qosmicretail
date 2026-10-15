namespace Mercurius.Shared.Admin
{
    // Values that actually occur in this tenant's audit log, for the filter dropdowns.
    public class AuditTrailFiltersDto
    {
        public List<string> Actions { get; set; } = new();
        public List<string> Sources { get; set; } = new();
        public List<string> EntityTypes { get; set; } = new();
        public List<string> Branches { get; set; } = new();
    }

    public class AuditLogListItemDto
    {
        public long Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string? UserName { get; set; }
        public string? LocationName { get; set; }
        public string Source { get; set; } = string.Empty;
        public string? DeviceName { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? Summary { get; set; }
        public string? IpAddress { get; set; }
    }

    public class AuditLogPageDto
    {
        public List<AuditLogListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class AuditLogDetailDto
    {
        public long Id { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public int? LocationId { get; set; }
        public string? LocationName { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? Summary { get; set; }
        // JSON: {field: {old, new}} for updates, {field: value} otherwise.
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? HttpMethod { get; set; }
        public string? RequestPath { get; set; }
        // Shared by every row one request produced (e.g. a sale's invoice, lines and stock changes).
        public string? CorrelationId { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? AppVersion { get; set; }
    }
}
