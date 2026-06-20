using System.ComponentModel.DataAnnotations;

namespace Mercurius.Common
{
    public class LoginRequest
    {
        [Required]
        [StringLength(256)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string Password { get; set; } = string.Empty;

        [StringLength(1024)]
        public string? DeviceInfo { get; set; }
    }
}
