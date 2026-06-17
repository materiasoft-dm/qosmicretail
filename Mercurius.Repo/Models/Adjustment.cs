using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Adjustment
{
    [Key]
    public int Id { get; set; }
public DateTime AdjustmentDate { get; set; }

    public int ReasonId { get; set; }

    public decimal Quantity { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime CreatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }
public DateTime? UpdatedDate { get; set; }

    public int ProductId { get; set; }
public string Note { get; set; }

    public int LocationId { get; set; }

    public bool IsActive { get; set; }
public string Filenames { get; set; }
public virtual Product Product { get; set; }
public virtual AdjustmentReason Reason { get; set; }
}
