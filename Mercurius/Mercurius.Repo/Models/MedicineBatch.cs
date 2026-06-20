using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public class MedicineBatch
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }

    [Required]
    public string BatchNumber { get; set; } = "";

    [Required]
    public DateTime ExpiryDate { get; set; }

    public DateTime ReceivedDate { get; set; }

    public decimal UnitCost { get; set; }

    public decimal InitialQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public int? StockReceiptId { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual Product Product { get; set; } = null!;
}
