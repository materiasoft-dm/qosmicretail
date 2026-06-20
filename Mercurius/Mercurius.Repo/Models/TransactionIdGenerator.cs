using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class TransactionIdGenerator
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
public string Name { get; set; }

    [StringLength(10)]
public string Prefix { get; set; }

    public int CurrentCount { get; set; }

    public int? Padding { get; set; }

    [StringLength(10)]
public string Suffix { get; set; }
}
