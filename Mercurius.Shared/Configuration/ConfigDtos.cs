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

    public class LocationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? MobilePhone { get; set; }
    }
    public class CreateLocationRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? MobilePhone { get; set; }
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
    }
    public class CreateCategoryFieldRequest
    {
        public int CategoryId { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
    }

}
