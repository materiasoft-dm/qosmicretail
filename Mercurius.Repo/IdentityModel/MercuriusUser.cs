using Microsoft.AspNetCore.Identity;

namespace Mercurius.Repo.IdentityModel
{
    public class MercuriusUser : IdentityUser
    {
        public bool IsActive { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName
        {
            get => $"{LastName}, {FirstName}";
        }
    }
}
