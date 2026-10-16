namespace Mercurius.Models;

public class ProductCsvRecord
{
    public string? ProductCode { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal StockQuantity { get; set; }
    public string? Barcode { get; set; }
    // Null when the file has no low-stock column (or leaves it blank): keep the product's own.
    public decimal? LowStock { get; set; }
}
