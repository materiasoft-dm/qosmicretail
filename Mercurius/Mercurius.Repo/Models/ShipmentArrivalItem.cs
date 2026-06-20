using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class ShipmentArrivalItem
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int ShipmentArrivalId { get; set; }
public decimal ItemCount { get; set; }
public decimal CostPrice { get; set; }
public decimal SalesPrice { get; set; }
public virtual Product Product { get; set; }
public virtual ShipmentArrival ShipmentArrival { get; set; }
}
