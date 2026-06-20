using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Supplier
{
    [Key]
    public int Id { get; set; }

    [Required]
public string Name { get; set; }

    public bool IsActive { get; set; }
public virtual ICollection<ShipmentArrival> ShipmentArrivals { get; set; } = new List<ShipmentArrival>();
}
