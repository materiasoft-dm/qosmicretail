using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;
public partial class BranchContactInformation
{
    [Key]
    public int Id { get; set; }

    public int BranchId { get; set; }

    public int ContactInformationId { get; set; }
public virtual Branch Branch { get; set; }
public virtual ContactInformation ContactInformation { get; set; }
}
