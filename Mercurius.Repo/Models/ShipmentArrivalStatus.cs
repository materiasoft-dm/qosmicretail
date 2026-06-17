using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;
public partial class ShipmentArrivalStatus
{
    [Key]
    public int Id { get; set; }

    [Required]
public string Name { get; set; }

    public bool IsActive { get; set; }

    [StringLength(1000)]
public string CssClass { get; set; }
public virtual ICollection<ShipmentArrival> ShipmentArrivals { get; set; } = new List<ShipmentArrival>();
}
