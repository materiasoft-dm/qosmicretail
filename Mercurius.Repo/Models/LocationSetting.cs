using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;
public partial class LocationSetting
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
public string SettingCode { get; set; }

    [Required]
public string SettingValue { get; set; }

    public int LocationId { get; set; }
}
