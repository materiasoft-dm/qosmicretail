using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class UserCurrentLocation
{
    [Key]
    public string UserId { get; set; }

    public int LocationId { get; set; }
}
