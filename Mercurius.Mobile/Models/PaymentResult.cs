namespace Mercurius.Mobile.Models;

public enum PaymentMethod
{
    Cash,
    Card
}

public class PaymentResult
{
    public PaymentMethod Method { get; set; }
    public decimal AmountReceived { get; set; }
    public decimal Change { get; set; }
}
