using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;
public partial class UserInformation
{
    [Key]
    public int Id { get; set; }

    public Guid IdentityUserId { get; set; }

    [Required]
public string FirstName { get; set; }
public string LastName { get; set; }
}
