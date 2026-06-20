using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Province
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
public string Name { get; set; }

    [Required]
    [StringLength(100)]
public string Region { get; set; }

    [Required]
    [StringLength(100)]
public string Code { get; set; }
}
