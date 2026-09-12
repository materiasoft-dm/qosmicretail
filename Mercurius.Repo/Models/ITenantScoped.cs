namespace Mercurius.Repo.Models
{
    // Marker for every entity that lives inside a Tenant's data boundary. MercuriusDbContext
    // applies one global query filter to every type implementing this, generically, via
    // reflection over ModelBuilder.Entity<T>() at model-creation time — see
    // MULTITENANCY_ARCHITECTURE.md §4.1. Implementing this is the only step a new entity needs;
    // no controller code has to remember to filter by TenantId.
    public interface ITenantScoped
    {
        int TenantId { get; set; }
    }
}
