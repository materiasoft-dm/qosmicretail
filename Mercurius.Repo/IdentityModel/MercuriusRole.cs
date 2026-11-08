using Microsoft.AspNetCore.Identity;

namespace Mercurius.Repo.IdentityModel
{
    // Roles belong to a tenant — see MULTITENANCY_ARCHITECTURE.md §3.4. Each tenant has its own
    // Administrator/Manager/User (and whatever roles its admins create), so editing a role's
    // permissions in one business never touches another's.
    //
    // TenantId is null only for platform roles that sit outside every tenant (today just
    // ModuleRegistry.DatabaseAdministratorRole, whose permissions reach every tenant's data).
    // MercuriusDbContext filters roles to "the current tenant's, plus platform roles", so role
    // lookups by name or id through RoleManager/UserManager only ever see those. Not
    // ITenantScoped: that interface's TenantId is non-nullable, and roles get their own filter.
    public class MercuriusRole : IdentityRole
    {
        public MercuriusRole()
        {
        }

        public MercuriusRole(string roleName) : base(roleName)
        {
        }

        public int? TenantId { get; set; }
    }
}
