using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class ContactInformation
{
    [Key]
    public int Id { get; set; }
public string PhoneNumber { get; set; }
public string EmailAddress { get; set; }
public string MobilePhoneNumber { get; set; }
public string Website { get; set; }
public string Facebook { get; set; }
public string Instagram { get; set; }
public string LinkedIn { get; set; }
public string Twitter { get; set; }
public virtual ICollection<BranchContactInformation> BranchContactInformations { get; set; } = new List<BranchContactInformation>();
public virtual ICollection<CustomerContactInformation> CustomerContactInformations { get; set; } = new List<CustomerContactInformation>();
public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
}
