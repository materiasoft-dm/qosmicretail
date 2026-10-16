namespace Mercurius.Shared.Invoices
{
    public class InvoiceListItemDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime? InvoiceDueDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public decimal? PaidAmount { get; set; }
        public bool HasRefund { get; set; }
    }

    public class InvoiceListResultDto
    {
        public List<InvoiceListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class InvoiceItemRowDto
    {
        public int InvoiceItemId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public decimal QuantityRefunded { get; set; }
        public decimal SalePrice { get; set; }
        public decimal QuantityRemaining => QuantitySold - QuantityRefunded;
    }

    // One refund (an InvoiceRefund and its lines) on the Refunds page.
    public class RefundListItemDto
    {
        public int Id { get; set; }
        public string RefundNumber { get; set; } = string.Empty;
        public DateTime DateRefunded { get; set; }
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        // e.g. "Paracetamol 500mg" or "Paracetamol 500mg +2 more".
        public string Products { get; set; } = string.Empty;
        public int LineCount { get; set; }
        public decimal Quantity { get; set; }
        // What the refunded quantity was sold for (custom line totals respected).
        public decimal Amount { get; set; }
        public string Reasons { get; set; } = string.Empty;
        public int RestockedLines { get; set; }
        public string RefundedBy { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class RefundListResultDto
    {
        public List<RefundListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class RefundHistoryRowDto
    {
        public string RefundNumber { get; set; } = string.Empty;
        public DateTime DateRefunded { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string ReasonName { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public bool WasRestocked { get; set; }
    }

    public class RefundReasonOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class InvoiceDetailDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public decimal? PaidAmount { get; set; }
        public List<InvoiceItemRowDto> Items { get; set; } = new();
        public List<RefundHistoryRowDto> RefundHistory { get; set; } = new();
        public List<RefundReasonOptionDto> ActiveRefundReasons { get; set; } = new();
    }

    public class CreateRefundRequest
    {
        public int InvoiceItemId { get; set; }
        public decimal Quantity { get; set; }
        public int RefundReasonId { get; set; }
        public string? Notes { get; set; }
        public bool Restock { get; set; }
    }
}
