namespace Mercurius.Client
{
    // Result carried back from PaymentDialog so Sell.razor can show the right confirmation —
    // "sale complete" vs. "saved offline, will sync later" look and mean different things to a
    // cashier even though both leave the cart empty.
    public class SaleOutcome
    {
        public bool SavedOffline { get; set; }
        public string? InvoiceNumber { get; set; }
    }
}
