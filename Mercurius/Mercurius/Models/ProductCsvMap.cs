using CsvHelper.Configuration;

namespace Mercurius.Models;

/// <summary>
/// CSV column mapping for product import. Supports multiple header name
/// variants including Loyverse POS exports where column names may include
/// store-specific suffixes like "Price [Store Name]".
/// </summary>
public class ProductCsvMap : ClassMap<ProductCsvRecord>
{
    public ProductCsvMap()
    {
        // SKU / Part Code
        Map(m => m.PartCode).Name("SKU", "sku", "PartCode", "partcode", "Part", "Code", "Handle", "Variant SKU");

        // Name
        Map(m => m.Name).Name("Name", "name", "Product", "product", "ProductName", "Title");

        // Description
        Map(m => m.Description).Name("Description", "description", "Desc", "Body HTML").Optional();

        // Category — Loyverse uses "Product Type" or "Type" sometimes
        Map(m => m.Category).Name("Category", "category", "CategoryName", "Product Type", "Type").Optional();

        // Cost Price
        Map(m => m.CostPrice).Name("CostPrice", "costprice", "Cost", "cost", "CurrentCostPrice", "Cost per item").Default(0m);

        // Sale Price — Loyverse: "Price [Store Name]" — PrepareHeaderForMatch strips the suffix
        Map(m => m.SalePrice).Name("SalePrice", "saleprice", "Price", "price", "SellingPrice", "CurrentSalePrice", "Variant Price").Optional().Default(0m);

        // Stock Quantity — Loyverse: "In stock [Store Name]" or "Available for sale [Store Name]"
        Map(m => m.StockQuantity).Name("StockQuantity", "stockquantity", "Stock", "stock", "Quantity", "CurrentStock", "Instock", "Availableforsale", "Variant Inventory Qty").Optional().Default(0m);
    }
}
