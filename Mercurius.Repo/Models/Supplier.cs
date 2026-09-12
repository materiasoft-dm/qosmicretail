using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Supplier : ITenantScoped
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required]
public string Name { get; set; }

    public bool IsActive { get; set; }
public virtual ICollection<ShipmentArrival> ShipmentArrivals { get; set; } = new List<ShipmentArrival>();
}
