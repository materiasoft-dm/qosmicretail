namespace Mercurius.Shared.Configuration
{
    public class AdjustmentReasonDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsInbound { get; set; }
    }
    public class CreateAdjustmentReasonRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsInbound { get; set; }
    }
    // IsActive=false is this app's "delete" (soft delete — see CLAUDE.md), so every update request
    // carries it rather than there being a separate DELETE endpoint.
    public class UpdateAdjustmentReasonRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsInbound { get; set; }
        public bool IsActive { get; set; }
    }

    public class ProductCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
    public class CreateProductCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
    public class UpdateProductCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    // A Location owns an Address and a ContactInformation row; the DTO flattens the fields the
    // Locations page edits (the same set the MVC Locations/Edit form had).
    public class LocationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? MobilePhone { get; set; }
        public string? PhoneNumber { get; set; }
        public string? EmailAddress { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? CityTown { get; set; }
        public string? Province { get; set; }
        public string? Country { get; set; }
        // Dashboard targets, as numbers of sales. Null means the default (10).
        public decimal? MonthlyTargetSalesCount { get; set; }
        public int? DailySalesTarget { get; set; }
        public bool IsActive { get; set; }
    }
    public class CreateLocationRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? MobilePhone { get; set; }
        public string? PhoneNumber { get; set; }
        public string? EmailAddress { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? CityTown { get; set; }
        public string? Province { get; set; }
        public string? Country { get; set; }
        public decimal? MonthlyTargetSalesCount { get; set; }
        public int? DailySalesTarget { get; set; }
    }
    // IsActive=false is the (soft) delete.
    public class UpdateLocationRequest : CreateLocationRequest
    {
        public bool IsActive { get; set; }
    }

    public class CategoryFieldDto
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Options { get; set; }
        public bool IsRequired { get; set; }
        public bool IsActive { get; set; }
    }
    public class CreateCategoryFieldRequest
    {
        public int CategoryId { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        // Comma-separated choices for "select" fields.
        public string? Options { get; set; }
        public bool IsRequired { get; set; }
    }
    public class UpdateCategoryFieldRequest : CreateCategoryFieldRequest
    {
        public bool IsActive { get; set; }
    }

    public class RefundReasonDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
    public class CreateRefundReasonRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
    public class UpdateRefundReasonRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
}
