using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public partial class RefundReason : ITenantScoped
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; }
    public string Description { get; set; }

    public bool IsActive { get; set; }
    public virtual ICollection<InvoiceItemRefund> InvoiceItemRefunds { get; set; } = new List<InvoiceItemRefund>();
}
