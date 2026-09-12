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
}
