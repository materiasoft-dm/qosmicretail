using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

/// <summary>
/// Stores a single custom field value for a product, linked to a
/// CategoryField definition that specifies the field's type, label, and
/// validation rules. Each category has its own set of CategoryField
/// definitions; each product stores its values here.
/// </summary>
public class ProductField
{
    [Key]
    public int Id { get; set; }

    /// <summary>FK to Product.Id — which product this value belongs to.</summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>FK to CategoryField.Id — which field definition this fills.</summary>
    [Required]
    public int CategoryFieldId { get; set; }

    /// <summary>The actual value as a string. Cast/parse based on CategoryField.FieldType.</summary>
    public string Value { get; set; } = "";
}
