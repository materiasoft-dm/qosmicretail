using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class InvoiceRefund
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
public string RefundNumber { get; set; }
public DateTime DateRefunded { get; set; }

    public Guid RefundedByUserId { get; set; }

    public int InvoiceId { get; set; }
public DateTime CreatedDate { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }
public string Notes { get; set; }

    public bool IsActive { get; set; }
public virtual ICollection<InvoiceItemRefund> InvoiceItemRefunds { get; set; } = new List<InvoiceItemRefund>();
}
