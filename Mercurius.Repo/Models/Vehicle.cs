using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Vehicle
{
    [Key]
    public int VehicleId { get; set; }

    [Required]
public string Model { get; set; }

    public int? Displacement { get; set; }

    public int? YearModel { get; set; }
public string ChassisNumber { get; set; }
public string EngineNumber { get; set; }
public string Make { get; set; }
public string Color { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime CreatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }
public DateTime? UpdatedDate { get; set; }

    public bool IsActive { get; set; }
}
