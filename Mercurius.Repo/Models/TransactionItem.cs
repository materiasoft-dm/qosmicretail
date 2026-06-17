using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class TransactionItem
{
    [Key]
    public int Id { get; set; }

    public int TransactionId { get; set; }

    public int ProductId { get; set; }

    /// <summary>
    /// Price how much the item was bought by the company (Per item)
    /// </summary>
public decimal? CostPrice { get; set; }

    /// <summary>
    /// Price how much the item is being sold by the company (Per item)
    /// </summary>
public decimal SalesPrice { get; set; }
public string Note { get; set; }
public virtual Transaction Transaction { get; set; }
}
