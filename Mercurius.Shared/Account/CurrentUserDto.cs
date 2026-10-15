namespace Mercurius.Shared.Account
{
    public class CurrentUserDto
    {
        public bool IsAuthenticated { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> Pages { get; set; } = new();
        public int TenantId { get; set; }
        public string? TenantName { get; set; }
        // May manage tenants (Platform Admin) — a flag on the user, never a role permission.
        public bool IsPlatformAdmin { get; set; }
        // The branch this user is working in (stock, sales and dashboards follow it), and the
        // branches they can switch to.
        public int? CurrentLocationId { get; set; }
        public string? CurrentLocationName { get; set; }
        public List<BranchOptionDto> Branches { get; set; } = new();
        // Daily database snapshots exist only on the SQLite provider.
        public bool DatabaseBackupsEnabled { get; set; }
    }

    public class BranchOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class SwitchBranchRequest
    {
        public int LocationId { get; set; }
    }

    // The signed-in user's own editable details (Account settings).
    public class AccountProfileDto
    {
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class UpdateAccountProfileRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
