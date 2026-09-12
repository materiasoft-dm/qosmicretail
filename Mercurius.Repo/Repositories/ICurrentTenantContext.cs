namespace Mercurius.Repo.Repositories
{
    // Resolves the current request's tenant boundary. MercuriusDbContext takes this as a
    // constructor dependency and references it inside the global query filter it applies to
    // every ITenantScoped entity — see MULTITENANCY_ARCHITECTURE.md §4.1. Lives here (not in the
    // web project) because MercuriusDbContext itself needs to depend on it; the concrete
    // implementation (which reads an HttpContext claim) lives in Mercurius, the only project that
    // has an HttpContext to read.
    public interface ICurrentTenantContext
    {
        // Deliberately fails closed: an unresolvable tenant (no claim, no HttpContext — e.g. a
        // background service or a design-time migration) must produce an id that matches no real
        // Tenant row, so a filter bug shows up as "sees nothing" rather than "sees everything".
        int TenantId { get; }
    }
}
