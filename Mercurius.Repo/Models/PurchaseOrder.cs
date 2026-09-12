using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

/// <summary>
/// Purchase Order — a list of products to be ordered from a supplier.
/// Status workflow: Pending Approval → Approved → Order Sent → Received Complete / Received Incomplete.
/// </summary>
public class PurchaseOrder : ITenantScoped
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>FK to Supplier.</summary>
    [Required]
    public int SupplierId { get; set; }

    /// <summary>Auto-generated order number, e.g. "PO-20260519-001".</summary>
    // Not [Required]: this is always server-generated in PurchaseOrdersController.Create, after
    // ModelState validation already runs — the Create form has no field for it, so it binds to
    // "" and would fail [Required] before the controller ever gets a chance to set it, silently
    // stranding the user on the Create page with no visible error (the view has no validation
    // summary either). Confirmed via Mercurius.E2ETests.PurchaseOrderTests.
    public string OrderNumber { get; set; } = "";

    /// <summary>Status: PendingApproval, Approved, OrderSent, ReceivedComplete, ReceivedIncomplete.</summary>
    [Required]
    public string Status { get; set; } = "PendingApproval";

    /// <summary>Date the order was created.</summary>
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    /// <summary>Expected delivery date.</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>Any notes or special instructions.</summary>
    public string? Notes { get; set; }

    /// <summary>Who created this order.</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>Who approved this order.</summary>
    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    /// <summary>Who marked this as sent.</summary>
    public Guid? SentBy { get; set; }

    public DateTime? SentDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual Supplier Supplier { get; set; } = null!;
    public virtual ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

/// <summary>
/// Line item on a purchase order — one product with quantity and cost.
/// </summary>
public class PurchaseOrderItem : ITenantScoped
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>FK to PurchaseOrder.</summary>
    [Required]
    public int PurchaseOrderId { get; set; }

    /// <summary>FK to Product.</summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>Quantity to order.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Estimated cost per unit from the supplier.</summary>
    public decimal? EstimatedUnitCost { get; set; }

    /// <summary>Quantity actually received (updated on shipment arrival).</summary>
    public decimal? ReceivedQuantity { get; set; }

    public string? Notes { get; set; }

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
