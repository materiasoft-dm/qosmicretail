using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

/// <summary>
/// Defines a custom field that appears on product forms when a specific
/// category is selected. Values are stored as JSON in Product.CustomFields.
/// </summary>
public class CategoryField
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [Required]
    public string FieldName { get; set; } = "";

    public string DisplayLabel { get; set; } = "";

    public string FieldType { get; set; } = "text";

    public string? Options { get; set; }

    public int SortOrder { get; set; }

    public bool IsRequired { get; set; }
}
