using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class ItemMovement
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
public string MovementType { get; set; }

    [StringLength(50)]
public string Direction { get; set; }

    public decimal? CountAfterTransaction { get; set; }

    public decimal? CountBeforeTransaction { get; set; }

    public decimal? Quantity { get; set; }
public string ItemName { get; set; }

    public int? LocationId { get; set; }

    public int? ProductId { get; set; }
public DateTime? TransactionDate { get; set; }

    [StringLength(100)]
public string TransactionId { get; set; }
}
