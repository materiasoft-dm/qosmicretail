namespace Mercurius.Mobile.Models;

public class ReceiptItemRow
{
    public string ProductName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal SalePrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal RefundedQuantity { get; set; }

    public string QuantityDisplay => $"{Quantity:0.##} x ₱{SalePrice:N2}";
    public string LineTotalDisplay => $"₱{LineTotal:N2}";
    public bool HasRefund => RefundedQuantity > 0;
    public string RefundDisplay => $"{RefundedQuantity:0.##} refunded";
}
