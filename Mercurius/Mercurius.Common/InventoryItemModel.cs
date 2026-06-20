namespace Mercurius.Common
{
    public class InventoryItemModel
    {
        public string ProductName { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Direction { get; set; } = "";
        public DateTime Date { get; set; }
    }
}
