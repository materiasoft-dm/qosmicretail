using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class InvoiceItemRefund : ITenantScoped
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int InvoiceItemId { get; set; }

    public int InvoiceId { get; set; }
public DateTime DateRefunded { get; set; }

    public decimal Quantity { get; set; }

    // Optional free-text detail alongside RefundReasonId — the reason dropdown is what's
    // actually required/reportable; this is just extra context for that specific line.
    public string Remarks { get; set; }

    public int InvoiceRefundId { get; set; }

    public int ProductId { get; set; }

    public int RefundReasonId { get; set; }

    // Whether this refund put the quantity back into inventory (via a linked Adjustment — see
    // Adjustment.InvoiceItemRefundId) — recorded here too so the refund record itself shows the
    // outcome without having to join back through Adjustments.
    public bool WasRestocked { get; set; }
public virtual InvoiceRefund InvoiceRefund { get; set; }
public virtual Product Product { get; set; }
public virtual RefundReason RefundReason { get; set; }
}
