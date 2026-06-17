using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class Transaction
{
    [Key]
    public int TransactionId { get; set; }
public DateTime? TransactionDate { get; set; }

    public int? CustomerId { get; set; }

    public Guid? TransactionBy { get; set; }

    public int TransactionStatusId { get; set; }
public string Note { get; set; }

    public Guid CreatedBy { get; set; }
public DateTime CreatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }
public DateTime? UpdatedDate { get; set; }
public virtual Customer Customer { get; set; }
public virtual ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
}
