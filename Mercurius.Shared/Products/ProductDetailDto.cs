namespace Mercurius.Shared.Products
{
    public class ProductDetailDto
    {
        public int Id { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ProductCategoryId { get; set; }
        public string? CategoryName { get; set; }
        public decimal? CurrentCostPrice { get; set; }
        public decimal MarkUpPercentage { get; set; }
        public decimal? CurrentSalePrice { get; set; }
        public int? LeadTimeDays { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal LowStockCount { get; set; }
        public string? Note { get; set; }
        public string? Model { get; set; }
        public string? CustomWarning { get; set; }
        public bool IsActive { get; set; }
        // Site-relative URL of the product photo, or null if none was uploaded.
        public string? ImageUrl { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        // The category's custom fields (e.g. Generic name, Dosage form for Medicines), each with
        // this product's saved value.
        public List<ProductCustomFieldDto> CustomFields { get; set; } = new();
    }

    // A per-category custom field definition, plus this product's value for it (if any).
    public class ProductCustomFieldDto
    {
        public int CategoryFieldId { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        // text | number | select | checkbox | date | textarea
        public string FieldType { get; set; } = "text";
        public List<string> Options { get; set; } = new();
        public bool IsRequired { get; set; }
        public string? Value { get; set; }
    }

    public class CreateProductRequest
    {
        public string ProductCode { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ProductCategoryId { get; set; }
        public decimal? CurrentCostPrice { get; set; }
        public decimal MarkUpPercentage { get; set; }
        public decimal? CurrentSalePrice { get; set; }
        public int? LeadTimeDays { get; set; }
        public decimal LowStockCount { get; set; }
        public string? Note { get; set; }
        // CategoryFieldId -> value, for the chosen category's custom fields.
        public Dictionary<int, string?> CustomFieldValues { get; set; } = new();
    }

    public class UpdateProductRequest
    {
        public string ProductCode { get; set; } = string.Empty;
        public string? Barcode { get; set; }
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
        public Dictionary<int, string?> CustomFieldValues { get; set; } = new();
    }

    public class ProductImportStartedDto
    {
        public string JobId { get; set; } = string.Empty;
    }

    // Progress of a background CSV import (ProductImportBackgroundService), polled by the page.
    public class ProductImportStatusDto
    {
        // Queued | Parsing | Importing | Completed | Failed
        public string Status { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Processed { get; set; }
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public List<string> Log { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
}
