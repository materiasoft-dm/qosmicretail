namespace Mercurius.Shared.Admin
{
    public class RoleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class RoleDetailDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> SelectedPages { get; set; } = new();
    }

    public class CreateRoleRequest
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Pages { get; set; } = new();
    }

    public class UpdateRolePagesRequest
    {
        public List<string> Pages { get; set; } = new();
    }

    public class UserListItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class SetUserActiveRequest
    {
        public bool Activate { get; set; }
    }

    public class AssignUserRolesRequest
    {
        public List<string> RoleIds { get; set; } = new();
    }

    public class LogFileDto
    {
        public string FileName { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public long Size { get; set; }
    }

    public class LogLineDto
    {
        public string Text { get; set; } = string.Empty;
        public bool IsError { get; set; }
        public bool IsWarning { get; set; }
        public bool IsInfo { get; set; }
        public bool IsDebug { get; set; }
    }
}
