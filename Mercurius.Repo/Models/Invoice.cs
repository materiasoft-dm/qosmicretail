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

    // ---- BIR invoice data. Set by the till that issued the invoice (the mobile POS computes it
    // with Mercurius.Shared.Bir.BirTaxCalculator and prints it, so the server stores what was
    // printed). Null/zero on invoices from before this existed.
    public int? PosTerminalId { get; set; }
    // The invoice's serial number within its terminal (printed as 8 digits). InvoiceNumber is
    // "<terminal code>-<serial>" for these, which keeps it unique across terminals.
    public long? TerminalInvoiceSequence { get; set; }
    public decimal VatableSales { get; set; }
    public decimal VatAmount { get; set; }
    public decimal VatExemptSales { get; set; }
    public decimal ZeroRatedSales { get; set; }
    // VAT removed because a senior/PWD-type discount made the sale VAT-exempt.
    public decimal LessVat { get; set; }
    public decimal DiscountAmount { get; set; }
    // The tenant's DiscountType applied to the whole ticket, plus a snapshot of how it read at the
    // time ("SC Disc 20%"): the discount type can be edited or deactivated later.
    public int? DiscountTypeId { get; set; }
    [StringLength(100)]
    public string? DiscountName { get; set; }
    // Mercurius.Shared.Bir.SpecialDiscountType: the discount's BIR reporting category; 0 = none/other.
    public int SpecialDiscountType { get; set; }
    [StringLength(50)]
    public string? SpecialDiscountIdNumber { get; set; }
    [StringLength(200)]
    public string? SpecialDiscountHolderName { get; set; }
    [StringLength(20)]
    public string? SpecialDiscountHolderTin { get; set; }
    [StringLength(200)]
    public string? BuyerName { get; set; }
    [StringLength(20)]
    public string? BuyerTin { get; set; }
    [StringLength(500)]
    public string? BuyerAddress { get; set; }
    [StringLength(200)]
    public string? BuyerBusinessStyle { get; set; }
    // The e-journal copy: the invoice exactly as printed, and its link in the terminal's hash
    // chain (SHA-256 of the previous invoice's hash + this text), which makes a deleted or edited
    // invoice detectable.
    public string? ReceiptText { get; set; }
    [StringLength(64)]
    public string? JournalHash { get; set; }
    // Cash | Card. Used to live only in Notes ("Paid by cash"/"Paid by card"), which reports
    // can't rely on; null only for invoices made before either was recorded.
    [StringLength(20)]
    public string? PaymentMethod { get; set; }
public virtual Customer Customer { get; set; }
    public virtual PosTerminal? PosTerminal { get; set; }
    public virtual DiscountType? DiscountType { get; set; }
public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
public virtual InvoiceStatus Status { get; set; }
}
