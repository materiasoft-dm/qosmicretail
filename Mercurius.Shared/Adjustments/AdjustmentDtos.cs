namespace Mercurius.Shared.Adjustments
{
    public class AdjustmentListItemDto
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string ReasonName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
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
        public string? Note { get; set; }
    }
}
