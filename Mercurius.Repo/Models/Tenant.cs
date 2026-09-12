using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models
{
    // The outermost data boundary — see MULTITENANCY_ARCHITECTURE.md. A tenant owns one or more
    // Locations (pharmacies, switchable via the existing UserCurrentLocation flow) and every
    // ITenantScoped row belongs to exactly one Tenant, enforced by a global EF Core query filter
    // (see MercuriusDbContext.OnModelCreating) rather than per-controller checks.
    public class Tenant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; }
    }
}
