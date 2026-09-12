namespace Mercurius.Shared.Products
{
    public class ProductListItemDto
    {
        public int Id { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public decimal Stock { get; set; }
        public decimal? SalePrice { get; set; }
        public decimal LowStockCount { get; set; }
        public bool IsActive { get; set; }
    }

    public class ProductListResultDto
    {
        public List<ProductListItemDto> Items { get; set; } = new();
        public int Total { get; set; }
    }
}
