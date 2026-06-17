using System;
using System.ComponentModel.DataAnnotations;

namespace Mercurius.Repo.Models
{
    public class UserDashboardLayout
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string UserId { get; set; } = string.Empty;

        [Required]
        public string WidgetId { get; set; } = string.Empty;

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
