using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Color
{
    [Key]
    public int Id { get; set; }

    [Required]
public string Name { get; set; }
public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
