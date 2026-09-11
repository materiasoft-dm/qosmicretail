namespace Mercurius.Mobile.Models;

public class ReceiptItemRow
{
    public string ProductName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal SalePrice { get; set; }

    public decimal LineTotal => Quantity * SalePrice;
    public string QuantityDisplay => $"{Quantity:0.##} x ₱{SalePrice:N2}";
    public string LineTotalDisplay => $"₱{LineTotal:N2}";
}
