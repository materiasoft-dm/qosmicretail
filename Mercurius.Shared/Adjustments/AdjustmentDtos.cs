namespace Mercurius.Shared.Adjustments
{
    public class AdjustmentListItemDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string ReasonName { get; set; } = string.Empty;
        // True when the reason adds stock; false when it takes stock away.
        public bool IsInbound { get; set; }
        public decimal Quantity { get; set; }
        public string? Note { get; set; }
        // Set when the adjustment restocked a refund — the refunded invoice.
        public int? InvoiceId { get; set; }
    }

    public class AdjustmentReasonOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsInbound { get; set; }
    }

    public class CreateAdjustmentRequest
    {
        public int ProductId { get; set; }
        public int ReasonId { get; set; }
        public decimal Quantity { get; set; }
        // When the stock actually moved (UTC); null means now.
        public DateTime? AdjustmentDate { get; set; }
        public string? Note { get; set; }
    }
}
