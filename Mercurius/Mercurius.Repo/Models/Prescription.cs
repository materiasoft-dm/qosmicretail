using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

/// <summary>
/// A prescription issued by a doctor for a patient. Tracks the prescribing doctor,
/// patient, date, and validity period. Linked to DispensedItems at dispensing time.
/// </summary>
public class Prescription
{
    [Key]
    public int Id { get; set; }

    /// <summary>Unique prescription number, e.g. "RX-20260518-001".</summary>
    [Required]
    public string PrescriptionNumber { get; set; } = "";

    /// <summary>FK to Doctor.</summary>
    [Required]
    public int DoctorId { get; set; }

    /// <summary>FK to Customer/Patient.</summary>
    [Required]
    public int PatientId { get; set; }

    /// <summary>Date the prescription was written.</summary>
    [Required]
    public DateTime PrescriptionDate { get; set; }

    /// <summary>Date after which the prescription is no longer valid.</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>Diagnosis or notes from the doctor.</summary>
    public string? Diagnosis { get; set; }

    /// <summary>Draft, Active, Dispensed, Expired, Cancelled.</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>Whether this prescription includes controlled substances (requires S2).</summary>
    public bool IsControlled { get; set; }

    /// <summary>Pharmacist who processed this prescription.</summary>
    public Guid? ProcessedBy { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public virtual Doctor Doctor { get; set; } = null!;
    public virtual Customer Patient { get; set; } = null!;
    public virtual ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
}

/// <summary>
/// A line item on a prescription — one medicine with dosage instructions.
/// </summary>
public class PrescriptionItem
{
    [Key]
    public int Id { get; set; }

    /// <summary>FK to Prescription.</summary>
    [Required]
    public int PrescriptionId { get; set; }

    /// <summary>FK to Product.</summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>Dosage instruction, e.g. "Take 1 tablet 3 times a day after meals".</summary>
    public string Dosage { get; set; } = "";

    /// <summary>Quantity prescribed (e.g. 30 tablets).</summary>
    public decimal QuantityPrescribed { get; set; }

    /// <summary>Quantity already dispensed.</summary>
    public decimal QuantityDispensed { get; set; }

    /// <summary>Duration in days, e.g. 7 for a 7-day course.</summary>
    public int? DurationDays { get; set; }

    public string? Notes { get; set; }

    public virtual Prescription Prescription { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
