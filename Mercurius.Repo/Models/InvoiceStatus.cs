using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;
public partial class InvoiceStatus
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
public string Name { get; set; }

    [StringLength(1000)]
public string CssClass { get; set; }
public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
