namespace Mercurius.Shared.PurchaseOrders
{
    public class PurchaseOrderListItemDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
    }

    public class CreatePurchaseOrderLineRequest
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal? EstimatedUnitCost { get; set; }
    }

    public class CreatePurchaseOrderRequest
    {
        public int SupplierId { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? Notes { get; set; }
        public List<CreatePurchaseOrderLineRequest> Lines { get; set; } = new();
    }

    // Only allowed while the order is still PendingApproval: editing after approval would change
    // what was approved. Lines replace the order's existing lines wholesale.
    public class UpdatePurchaseOrderRequest
    {
        public int SupplierId { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? Notes { get; set; }
        public List<CreatePurchaseOrderLineRequest> Lines { get; set; } = new();
    }

    public class PurchaseOrderLineDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal? EstimatedUnitCost { get; set; }
        public decimal? ReceivedQuantity { get; set; }
    }

    public class PurchaseOrderDetailDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public DateTime? SentDate { get; set; }
        public string? Notes { get; set; }
        public List<PurchaseOrderLineDto> Items { get; set; } = new();
    }
}
