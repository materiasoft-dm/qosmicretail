namespace Mercurius.Shared.Admin
{
    // Raw SQL console. The key is the "AdminQueryApiKey" secret, required on every request on top
    // of the ADMIN_DATA_QUERY permission.
    public class DataQueryRequest
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Sql { get; set; } = string.Empty;
    }

    public class DataQueryResultDto
    {
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
        // Meaningful for INSERT/UPDATE/DELETE; -1 for a SELECT.
        public int RecordsAffected { get; set; }
    }

    // Daily SQLite snapshots (DatabaseBackupService). Enabled is false under SQL Server, whose
    // hosting provider manages backups instead.
    public class DatabaseBackupsDto
    {
        public bool Enabled { get; set; }
        public List<DatabaseBackupDto> Backups { get; set; } = new();
    }

    public class DatabaseBackupDto
    {
        public string FileName { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public long SizeBytes { get; set; }
    }

    public class TenantDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? ContactNumber { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public int BranchCount { get; set; }
        public int UserCount { get; set; }
    }

    public class CreateTenantRequestDto
    {
        public string TenantName { get; set; } = string.Empty;
        public string FirstLocationName { get; set; } = "Main Branch";
        public string? Address { get; set; }
        public string? ContactNumber { get; set; }
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }

    // Editing a tenant's own details from its details dialog on the Tenants page.
    public class UpdateTenantRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? ContactNumber { get; set; }
    }
}
