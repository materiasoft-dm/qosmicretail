using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mercurius.Common
{
    public class UserSession
    {
        public string Username { get; set; } = "";
        public string Token { get; set; } = "";
        public string Role { get; set; } = "";
        public int ExpiresIn { get; set; }
        public DateTime ExpiryTimeStamp { get; set; }
        public List<string> Access { get; set; } = new();
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
