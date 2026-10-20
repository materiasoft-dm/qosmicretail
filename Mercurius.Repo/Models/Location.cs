using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Location : ITenantScoped
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required]
public string Name { get; set; }

    public int AddressId { get; set; }

    public int ContactInformationId { get; set; }
public decimal? MetaMonthlyTargetSalesCount { get; set; }

    public bool IsActive { get; set; } = true;

    // The branch as registered with BIR (its Certificate of Registration). Printed at the top of
    // every invoice and reading the POS issues for this branch. All optional until the pharmacy
    // registers; receipts say "NOT A VALID INVOICE" while they're missing.
    [StringLength(200)]
    public string? TaxRegisteredName { get; set; }
    [StringLength(200)]
    public string? TaxBusinessName { get; set; }
    [StringLength(500)]
    public string? TaxAddress { get; set; }
    // 9 digits; stored as typed.
    [StringLength(20)]
    public string? Tin { get; set; }
    // 5 digits, "00000" for the head office.
    [StringLength(10)]
    public string? TinBranchCode { get; set; }
    public bool IsVatRegistered { get; set; } = true;
public virtual Address Address { get; set; }
public virtual ContactInformation ContactInformation { get; set; }
}
