using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models;

public class DosageForm
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = "";

    public string? Description { get; set; }
}
