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
    }
}
