namespace Mercurius.Shared.Sales
{
    public class CartLineRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class CheckoutRequest
    {
        public int CustomerId { get; set; }
        public string? Notes { get; set; }
        public List<CartLineRequest> Items { get; set; } = new();
        public string PaymentMethod { get; set; } = "Cash";
        public decimal AmountReceived { get; set; }
    }

    public class CheckoutResultDto
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public int InvoiceId { get; set; }
        public decimal Total { get; set; }
        public decimal Change { get; set; }
    }

    public class CustomerOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // Lightweight snapshot of the catalog for Mercurius.Client's offline product cache (IndexedDB)
    // — deliberately not paginated, since the whole point is to have the full catalog available
    // locally before connectivity drops.
    public class OfflineProductDto
    {
        public int Id { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string? Category { get; set; }
        public decimal? SalePrice { get; set; }
        public decimal Stock { get; set; }
        public decimal LowStockCount { get; set; }
    }

    // A sale rung up while offline, queued client-side and replayed once back online.
    public class OfflineSaleRequest : CheckoutRequest
    {
        public Guid SyncId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
