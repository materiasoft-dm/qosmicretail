using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class ShipmentArrival
{
    [Key]
    public int Id { get; set; }

    public int? SupplierId { get; set; }

    public int ShipmentArrivalStatusId { get; set; }
public DateTime ShipmentArrivalDate { get; set; }

    public Guid? ShipmentReceivedBy { get; set; }
public DateTime CreatedDate { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public int LocationId { get; set; }
public string Notes { get; set; }
public string Filenames { get; set; }

    public bool IsActive { get; set; }

    [StringLength(500)]
public int? PurchaseOrderId { get; set; }

    [StringLength(500)]
public string TrackingNumber { get; set; }
public virtual ICollection<ShipmentArrivalItem> ShipmentArrivalItems { get; set; } = new List<ShipmentArrivalItem>();
public virtual ShipmentArrivalStatus ShipmentArrivalStatus { get; set; }
public virtual Supplier Supplier { get; set; }
public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
}
