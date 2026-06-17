using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Invoice
{
    [Key]
    public int Id { get; set; }
public DateTime InvoiceDate { get; set; }

    public int? CustomerId { get; set; }

    public int StatusId { get; set; }

    [StringLength(50)]
public string InvoiceNumber { get; set; }
public DateTime InvoiceDueDate { get; set; }

    public int LocationId { get; set; }
public string Notes { get; set; }
public DateTime CreatedDate { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime? UpdatedDate { get; set; }

    public Guid? UpdateBy { get; set; }

    public bool HasRefund { get; set; }
public decimal PaidAmount { get; set; }
public virtual Customer Customer { get; set; }
public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
public virtual InvoiceStatus Status { get; set; }
}
