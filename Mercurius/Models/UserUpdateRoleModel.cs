using Mercurius.Repo.IdentityModel;
using Microsoft.AspNetCore.Identity;

namespace Mercurius.Models
{
    public class UserUpdateRoleModel
    {
        public MercuriusUser User { get; set; } = null!;
        public List<IdentityRole> Roles { get; set; } = new();
    }
}
