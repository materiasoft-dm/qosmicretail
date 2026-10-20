namespace Mercurius.Client
{
    // Result carried back from PaymentDialog so Sell.razor can show the right confirmation —
    // "sale complete" vs. "saved offline, will sync later" look and mean different things to a
    // cashier even though both leave the cart empty.
    public class SaleOutcome
    {
        public bool SavedOffline { get; set; }
        public string? InvoiceNumber { get; set; }
        // Set when nothing was charged because the ticket's prices were out of date: the current
        // prices to put on the ticket before the cashier charges again.
        public Mercurius.Shared.Sales.CheckoutPriceChangeDto? PriceChange { get; set; }
    }
}
