namespace Mercurius.Shared.Products
{
    public class ProductDetailDto
    {
        public int Id { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ProductCategoryId { get; set; }
        public decimal? CurrentCostPrice { get; set; }
        public decimal MarkUpPercentage { get; set; }
        public decimal? CurrentSalePrice { get; set; }
        public int? LeadTimeDays { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal LowStockCount { get; set; }
        public string? Note { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateProductRequest
    {
        public string ProductCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ProductCategoryId { get; set; }
        public decimal? CurrentCostPrice { get; set; }
        public decimal MarkUpPercentage { get; set; }
        public decimal? CurrentSalePrice { get; set; }
        public int? LeadTimeDays { get; set; }
        public decimal LowStockCount { get; set; }
        public string? Note { get; set; }
        public bool IsActive { get; set; }
    }
}
