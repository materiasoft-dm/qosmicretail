namespace Mercurius.Shared.Shipments
{
    public class ShipmentListItemDto
    {
        public int Id { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string TrackingNumber { get; set; } = string.Empty;
        // How many units arrived (0 for a shipment recorded as pending, delayed or damaged).
        public decimal UnitsReceived { get; set; }
    }

    public class PurchaseOrderOptionDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
    }

    // One product that arrived. Receiving a shipment adds Quantity to the product's stock and
    // starts a batch for it at these prices (see StockReceiving).
    public class ShipmentItemRequest
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal UnitSalePrice { get; set; }
        // Blank: one is made up from the shipment (SHP-<id>). Expiry blank: the stock doesn't expire.
        public string? BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class CreateShipmentRequest
    {
        // Picked once per form, so submitting twice can't receive the stock twice.
        public Guid? SyncId { get; set; }
        // When it arrived (UTC; null means now) and its condition: Pending / Received / Delayed / Damaged.
        public DateTime? ArrivalDate { get; set; }
        public int? StatusId { get; set; }
        public int? SupplierId { get; set; }
        public int? PurchaseOrderId { get; set; }
        public string? TrackingNumber { get; set; }
        public string? Notes { get; set; }
        // What arrived. Required when the status is Received, and only allowed then: a shipment that
        // is pending, delayed or damaged hasn't put anything on the shelf.
        public List<ShipmentItemRequest> Items { get; set; } = new();
    }

    // A purchase order line, for filling in what was actually delivered.
    public class ReceivableLineDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public decimal Ordered { get; set; }
        public decimal AlreadyReceived { get; set; }
        public decimal UnitCost { get; set; }
        public decimal UnitSalePrice { get; set; }
    }

    // A product to receive that isn't on a purchase order.
    public class ReceivableProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public decimal UnitSalePrice { get; set; }
    }

    public class CreatedShipmentDto
    {
        public int Id { get; set; }
    }

    public class ShipmentStatusDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
