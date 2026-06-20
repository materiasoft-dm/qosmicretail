using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Location
{
    [Key]
    public int Id { get; set; }

    [Required]
public string Name { get; set; }

    public int AddressId { get; set; }

    public int ContactInformationId { get; set; }
public decimal? MetaMonthlyTargetSalesCount { get; set; }
public virtual Address Address { get; set; }
public virtual ContactInformation ContactInformation { get; set; }
}
