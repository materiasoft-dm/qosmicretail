using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class InvoiceItem
{
    [Key]
    public int Id { get; set; }

    // Stable cross-device identity for offline-first sync with the future mobile app — see
    // Product.SyncId for the rationale.
    public Guid SyncId { get; set; } = Guid.NewGuid();

    [Required]
    public int InvoiceId { get; set; }

    [Required]
    public int ProductId { get; set; }
public decimal Quantity { get; set; }
public decimal SalePrice { get; set; }
public decimal CostPrice { get; set; }
public string Remarks { get; set; }

    [Required]
    public int StatusId { get; set; }
public decimal? CustomTotalPrice { get; set; }

    public bool IsWholeSale { get; set; }

    public int? WholeSaleId { get; set; }

    // Which batch this line was actually priced/deducted from — null for products that aren't
    // batch-tracked (most non-drug items). Lets a refund credit the right batch and a lot recall
    // trace exactly which sales a given batch went into.
    public int? MedicineBatchId { get; set; }

public virtual Invoice Invoice { get; set; }
public virtual Product Product { get; set; }
public virtual InvoiceStatus Status { get; set; }
public virtual MedicineBatch? MedicineBatch { get; set; }
}
