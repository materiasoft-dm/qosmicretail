using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

/// <summary>
/// Prescribing doctor. Tracks PRC license, specialization, and S2 license
/// (required for prescribing controlled substances).
/// </summary>
public class Doctor
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string FirstName { get; set; } = "";

    public string? MiddleName { get; set; }

    [Required]
    public string LastName { get; set; } = "";

    /// <summary>PRC license number.</summary>
    public string? LicenseNumber { get; set; }

    /// <summary>PTR number (Philippines).</summary>
    public string? PtrNumber { get; set; }

    /// <summary>S2 license number for prescribing controlled substances.</summary>
    public string? S2LicenseNumber { get; set; }

    /// <summary>Specialization, e.g. "Pediatrics", "Internal Medicine".</summary>
    public string? Specialization { get; set; }

    public string? ContactNumber { get; set; }

    public string? EmailAddress { get; set; }

    /// <summary>Clinic or hospital affiliation.</summary>
    public string? ClinicName { get; set; }

    public string? ClinicAddress { get; set; }

    public string? ClinicContactNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
