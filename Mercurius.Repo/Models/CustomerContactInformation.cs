using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;
public partial class CustomerContactInformation
{
    [Key]
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int ContactInformationId { get; set; }

    public bool IsDefault { get; set; }
public virtual ContactInformation ContactInformation { get; set; }
public virtual Customer Customer { get; set; }
}
