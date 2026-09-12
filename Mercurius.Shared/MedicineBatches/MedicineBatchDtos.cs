namespace Mercurius.Shared.MedicineBatches
{
    public class MedicineBatchDto
    {
        public int Id { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public DateTime ReceivedDate { get; set; }
        public decimal UnitCost { get; set; }
        public decimal UnitSalePrice { get; set; }
        public decimal InitialQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
    }

    public class MedicineBatchListResultDto
    {
        public string ProductName { get; set; } = string.Empty;
        public List<MedicineBatchDto> Items { get; set; } = new();
    }

    public class CreateMedicineBatchRequest
    {
        public int ProductId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public decimal UnitCost { get; set; }
        public decimal UnitSalePrice { get; set; }
        public decimal InitialQuantity { get; set; }
    }
}
