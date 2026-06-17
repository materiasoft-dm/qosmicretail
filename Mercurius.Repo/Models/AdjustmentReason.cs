using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class AdjustmentReason
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
public string Name { get; set; }
public string Description { get; set; }

    public bool IsActive { get; set; }

    public bool IsInbound { get; set; }

    [StringLength(1000)]
public string CssClass { get; set; }
public virtual ICollection<Adjustment> Adjustments { get; set; } = new List<Adjustment>();
}
