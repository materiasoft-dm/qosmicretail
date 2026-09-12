using Microsoft.AspNetCore.Identity;
using Mercurius.Repo.Models;

namespace Mercurius.Repo.IdentityModel
{
    // Deliberately NOT ITenantScoped, unlike every other tenant-owned entity — see
    // MULTITENANCY_ARCHITECTURE.md §3.5 and the note on TenantId below. Applying the generic
    // global query filter here breaks sign-in itself: Identity's own SignInManager/UserManager
    // look a user up by email directly against the Users DbSet as the FIRST step of
    // authentication, before any tenant context exists (the login *is* what establishes it) — a
    // filtered lookup at that point can never find anyone, since ICurrentTenantContext has
    // nothing to resolve yet. Confirmed by hitting exactly this failure mode while building the
    // filter. Tenant-scoping *within* an authenticated session still works fine — it just has to
    // come from the TenantId claim on the signed-in principal (set at claims-generation time,
    // see MercuriusClaimsPrincipalFactory), not from filtering this table.
    public class MercuriusUser : IdentityUser
    {
        public bool IsActive { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName
        {
            get => $"{LastName}, {FirstName}";
        }

        // A user account lives inside exactly one tenant, set at creation and not switchable —
        // see MULTITENANCY_ARCHITECTURE.md §3.5. Switching pharmacies within that tenant is the
        // existing, unrelated UserCurrentLocation flow. Any admin screen that lists/manages other
        // users must filter by this explicitly (e.g. WHERE TenantId == current tenant) — it is
        // NOT enforced automatically the way Product/Location are.
        public int TenantId { get; set; }

        // Outside every tenant's boundary — the only accounts that can manage the Tenants table
        // itself (create/suspend tenants) or deliberately bypass another entity's tenant filter.
        // See §3.6/§4.3.
        public bool IsPlatformAdmin { get; set; }
    }
}
