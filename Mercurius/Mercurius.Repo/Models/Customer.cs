using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Customer
{
    [Key]
    public int Id { get; set; }

    [Required]
public string FirstName { get; set; }
public string MiddleName { get; set; }
public string LastName { get; set; }

    [StringLength(50)]
public string ContactNumber { get; set; }
public string EmailAddress { get; set; }
public string Tinnumber { get; set; }

    public int? AddressId { get; set; }

    public bool IsActive { get; set; }
public DateTime CreatedDate { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    // --- Patient fields ---
    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? BloodType { get; set; }

public virtual Address Address { get; set; }
public virtual ICollection<CustomerContactInformation> CustomerContactInformations { get; set; } = new List<CustomerContactInformation>();
public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
