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
        // When it arrived (UTC; null means now) and its condition: Pending / Received / Delayed / Damaged.
        public DateTime? ArrivalDate { get; set; }
        public int? StatusId { get; set; }
        public int? SupplierId { get; set; }
        public int? PurchaseOrderId { get; set; }
        public string? TrackingNumber { get; set; }
        public string? Notes { get; set; }
    }

    public class ShipmentStatusDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
