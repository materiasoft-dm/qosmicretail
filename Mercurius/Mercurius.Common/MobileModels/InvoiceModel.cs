using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mercurius.Common.MobileModels
{
    public class InvoiceModel
    {
        public int Id { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string CustomerName { get; set; } = "";
        public string Status { get; set; } = "";
        public string InvoiceNumber { get; set; } = "";
        public DateTime InvoiceDueDate { get; set; }
        public string Location { get; set; } = "";
        public string Notes { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime? UpdatedDate { get; set; }
        public string? UpdateBy { get; set; }
        public int ItemCount { get; set; }
        public List<InvoiceItemModel> InvoiceItems { get; set; } = new();
        public Guid CreatedById { get; set; }
        public Guid? UpdateById { get; set; }
    }

    public class InvoiceItemModel
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? PartCode { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string? Notes { get; set; }
    }
}
