using System;
using System.Collections.Generic;
using Mercurius.Repo.Models;

namespace Mercurius.Models
{
    public class InvoiceDetailsViewModel
    {
        public Invoice Invoice { get; set; } = null!;
        public List<InvoiceItemRow> Items { get; set; } = new();
        public List<RefundHistoryRow> RefundHistory { get; set; } = new();
        public List<RefundReason> ActiveRefundReasons { get; set; } = new();
    }

    public class InvoiceItemRow
    {
        public int InvoiceItemId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public decimal QuantityRefunded { get; set; }
        public decimal SalePrice { get; set; }
        public decimal QuantityRemaining => QuantitySold - QuantityRefunded;
    }

    public class RefundHistoryRow
    {
        public string RefundNumber { get; set; } = string.Empty;
        public DateTime DateRefunded { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string ReasonName { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public bool WasRestocked { get; set; }
    }
}
