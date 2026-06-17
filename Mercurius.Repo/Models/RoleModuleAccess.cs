using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;
public partial class RoleModuleAccess
{
    [Key]
    public int Id { get; set; }

    [Required]
public string RoleId { get; set; }

    [Required]
public string ModuleIdentifier { get; set; }
}
