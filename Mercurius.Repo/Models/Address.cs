using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Address
{
    [Key]
    public int Id { get; set; }
public string Line1 { get; set; }
public string Line2 { get; set; }
public string CityTown { get; set; }
public string Province { get; set; }
public string Country { get; set; }

    public bool IsActive { get; set; }

    public bool IsDefault { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime CreatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }
public DateTime? UpdatedDate { get; set; }
public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();
public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
}
