using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class File
{
    [Key]
    public int Id { get; set; }

    [Required]
    public byte[] FileContent { get; set; }
}
