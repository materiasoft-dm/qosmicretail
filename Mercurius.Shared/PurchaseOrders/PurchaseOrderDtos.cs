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
}
