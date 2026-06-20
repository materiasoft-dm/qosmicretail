using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class InvoiceItem
{
    [Key]
    public int Id { get; set; }

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
public virtual Invoice Invoice { get; set; }
public virtual Product Product { get; set; }
public virtual InvoiceStatus Status { get; set; }
}
