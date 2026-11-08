namespace Mercurius.Shared.Sales
{
    public class CartLineRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        // The unit price the ticket showed the customer. Checkout refuses the sale (409, with the
        // current prices) if the server would charge something else, so the cashier never charges
        // a total the customer wasn't shown; an offline sale replayed later keeps it, since the
        // customer already paid it. Null from tills older than this: the server's price is used.
        public decimal? UnitPrice { get; set; }
    }

    public class CheckoutRequest
    {
        // Chosen by the till once per sale, so a retried or replayed checkout (the first
        // response was lost, or the sale was queued offline) finds the sale already made
        // instead of charging it twice.
        public Guid? SyncId { get; set; }
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

    // 409 body from checkout when the ticket's prices are out of date (a batch ran out, or the
    // price changed since the page loaded). Nothing was saved.
    public class CheckoutPriceChangeDto
    {
        public string Error { get; set; } = string.Empty;
        public List<CartLinePriceDto> Prices { get; set; } = new();
    }

    public class CartLinePriceDto
    {
        public int ProductId { get; set; }
        public decimal UnitPrice { get; set; }
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
        public string? Barcode { get; set; }
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
        public DateTime CreatedAtUtc { get; set; }
    }
}
