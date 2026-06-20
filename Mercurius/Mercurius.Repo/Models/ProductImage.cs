using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

/// <summary>
/// Additional product images beyond the primary ImageFilename. Supports multi-image
/// galleries on the product edit/details pages. Position controls display order
/// (lower = first), IsPrimary flags the main image if ImageFilename is null.
/// </summary>
public partial class ProductImage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Stored filename on disk under wwwroot/ProductImages/, or a remote URL
    /// for images that were synced from Shopify without being downloaded.
    /// </summary>
    [Required]
    public string Filename { get; set; }

    /// <summary>
    /// True when the image lives on the local filesystem (wwwroot/ProductImages/),
    /// false when it's a remote URL (e.g. Shopify CDN).
    /// </summary>
    public bool IsLocal { get; set; } = true;

    /// <summary>
    /// Display order. Lower numbers render first. Position 0 is the default
    /// for newly added images.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Optional alt text for accessibility / SEO.
    /// </summary>
    public string? Alt { get; set; }

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    public virtual Product? Product { get; set; }
}
