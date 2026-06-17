using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Product
{
    [Key]
    public int Id { get; set; }

    [Required]
[Display(Name = "SKU")]
public string PartCode { get; set; }
public string Description { get; set; }

    public int? ProductCategoryId { get; set; }
public DateTime CreateDate { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsActive { get; set; }
public string Note { get; set; }
public string CustomWarning { get; set; }

    [Required]
public string Name { get; set; }
public string Model { get; set; }

    public int? SizeId { get; set; }

    public int? ColorId { get; set; }
public decimal? CurrentCostPrice { get; set; }
public decimal? CurrentSalePrice { get; set; }

    public int? LeadTimeDays { get; set; }

    // CurrentStock + LowStockCount widened from int to decimal (R11) so pharmacy products
    // can carry fractional inventory (10.5 ml vials, partial tablets). Existing integer values
    // round-trip without loss; LiteDB stores both numeric types in the same Bson field shape.
    public decimal CurrentStock { get; set; }
public string ImageFilename { get; set; }
public decimal MarkUpPercentage { get; set; }

    public decimal LowStockCount { get; set; }

public virtual ICollection<Adjustment> Adjustments { get; set; } = new List<Adjustment>();
public virtual ICollection<BulkPackage> BulkPackages { get; set; } = new List<BulkPackage>();
public virtual Color Color { get; set; }
public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
public virtual ProductCategory ProductCategory { get; set; }
public virtual ICollection<ShipmentArrivalItem> ShipmentArrivalItems { get; set; } = new List<ShipmentArrivalItem>();
public virtual ICollection<InvoiceItemRefund> InvoiceItemRefunds { get; set; } = new List<InvoiceItemRefund>();
public virtual Size Size { get; set; }
public virtual ICollection<MedicineBatch> MedicineBatches { get; set; } = new List<MedicineBatch>();
}
