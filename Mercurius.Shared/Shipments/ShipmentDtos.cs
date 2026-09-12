namespace Mercurius.Shared.Shipments
{
    public class ShipmentListItemDto
    {
        public int Id { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string TrackingNumber { get; set; } = string.Empty;
    }

    public class PurchaseOrderOptionDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
    }

    public class CreateShipmentRequest
    {
        public int? SupplierId { get; set; }
        public int? PurchaseOrderId { get; set; }
        public string? TrackingNumber { get; set; }
        public string? Notes { get; set; }
    }
}
