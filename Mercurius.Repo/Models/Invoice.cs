using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Invoice : ITenantScoped
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    // Stable cross-device identity for offline-first sync with the future mobile app — see
    // Product.SyncId for the rationale.
    public Guid SyncId { get; set; } = Guid.NewGuid();
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

    // Where the sale was rung up — shown on every terminal's Receipts list so staff can tell a
    // web sale from a mobile one, and which terminal. Web | MobileApp; null for invoices created
    // before this was recorded. Device fields are only set for MobileApp.
    [StringLength(20)]
    public string? SourceChannel { get; set; }

    [StringLength(100)]
    public string? SourceDeviceId { get; set; }

    [StringLength(200)]
    public string? SourceDeviceName { get; set; }
public decimal PaidAmount { get; set; }
    // Cash | Card. Used to live only in Notes ("Paid by cash"/"Paid by card"), which reports
    // can't rely on; null only for invoices made before either was recorded.
    [StringLength(20)]
    public string? PaymentMethod { get; set; }
public virtual Customer Customer { get; set; }
public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
public virtual InvoiceStatus Status { get; set; }
}
