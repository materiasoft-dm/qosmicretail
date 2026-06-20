using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class ProductCategory
{
    [Key]
    public int Id { get; set; }

    [Required]
public string Name { get; set; }
public string Description { get; set; }
public DateTime CreatedDate { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsActive { get; set; }
public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
