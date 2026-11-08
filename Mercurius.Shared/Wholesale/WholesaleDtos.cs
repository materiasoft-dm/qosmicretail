namespace Mercurius.Shared.Wholesale
{
    // A product as the wholesale order form offers it: what a line will be charged, and whether
    // that's a real wholesale price or the retail fallback (no wholesale price set).
    public class WholesaleProductOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public decimal? WholesalePrice { get; set; }
        public decimal? RetailPrice { get; set; }
        public decimal Stock { get; set; }
        public decimal UnitPrice => WholesalePrice ?? RetailPrice ?? 0m;
        public bool UsesRetailPrice => !WholesalePrice.HasValue;
    }

    public class WholesaleOrderLineRequest
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class CreateWholesaleOrderRequest
    {
        // Chosen by the form once per order, so a retried submit (the first response was lost)
        // returns the order already made instead of creating — and deducting stock for — a second.
        public Guid? SyncId { get; set; }
        public int CustomerId { get; set; }
        public DateTime? DueDate { get; set; }
        // Paid up front. Zero = all on credit; anything less than the total is a partial payment.
        public decimal AmountPaid { get; set; }
        public string? Notes { get; set; }
        public List<WholesaleOrderLineRequest> Lines { get; set; } = new();
    }

    public class CreatedWholesaleOrderDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
    }

    public class RecordWholesalePaymentRequest
    {
        public decimal Amount { get; set; }
    }

    public class WholesaleOrderListItemDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal PaidAmount { get; set; }
        // Total less refunds and payments; negative when the customer is owed money back.
        public decimal Balance { get; set; }
        public bool HasRefund { get; set; }
    }

    public class WholesaleOrderListResultDto
    {
        public List<WholesaleOrderListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
    }

    public class WholesaleOrderLineDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal QuantityRefunded { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }

    public class WholesaleOrderDetailDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Balance { get; set; }
        public List<WholesaleOrderLineDto> Lines { get; set; } = new();
    }
}
