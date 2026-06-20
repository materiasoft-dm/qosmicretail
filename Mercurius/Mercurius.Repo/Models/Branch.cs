using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Branch
{
    [Key]
    public int Id { get; set; }

    [Required]
public string Name { get; set; }
public string Description { get; set; }

    public int AddressId { get; set; }

    public bool IsActive { get; set; }
public virtual Address Address { get; set; }
public virtual ICollection<BranchContactInformation> BranchContactInformations { get; set; } = new List<BranchContactInformation>();
}
