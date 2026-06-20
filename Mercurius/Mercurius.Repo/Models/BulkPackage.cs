using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class BulkPackage
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int QuantityInPackage { get; set; }
public decimal SalePrice { get; set; }
public string Description { get; set; }

    public bool IsActive { get; set; }
public DateTime DateCreated { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime? DateUpdated { get; set; }

    public Guid? UpdatedBy { get; set; }

    public int LocationId { get; set; }
public string Filenames { get; set; }
public virtual Product Product { get; set; }
}
