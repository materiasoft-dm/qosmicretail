using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class InvoiceItemRefund
{
    [Key]
    public int Id { get; set; }

    public int InvoiceItemId { get; set; }

    public int InvoiceId { get; set; }
public DateTime DateRefunded { get; set; }

    public decimal Quantity { get; set; }

    [Required]
public string Remarks { get; set; }

    public int InvoiceRefundId { get; set; }

    public int ProductId { get; set; }
public virtual InvoiceRefund InvoiceRefund { get; set; }
public virtual Product Product { get; set; }
}
