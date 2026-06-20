using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Mercurius.Common.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static T? GetLoggedInUserId<T>(this ClaimsPrincipal principal)
        {
            if (principal == null)
                throw new ArgumentNullException(nameof(principal));

            var loggedInUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (typeof(T) == typeof(string))
            {
                return loggedInUserId is null ? default : (T?)(object)loggedInUserId;
            }
            else if (typeof(T) == typeof(int) || typeof(T) == typeof(long))
            {
                if (string.IsNullOrEmpty(loggedInUserId))
                    return default;
                return (T)Convert.ChangeType(loggedInUserId, typeof(T));
            }
            else
            {
                throw new InvalidOperationException($"Type {typeof(T).Name} is not supported by GetLoggedInUserId.");
            }
        }

        public static string? GetLoggedInUserName(this ClaimsPrincipal principal)
        {
            if (principal == null)
                throw new ArgumentNullException(nameof(principal));

            return principal.FindFirstValue(ClaimTypes.Name);
        }

        public static string? GetLoggedInUserEmail(this ClaimsPrincipal principal)
        {
            if (principal == null)
                throw new ArgumentNullException(nameof(principal));

            return principal.FindFirstValue(ClaimTypes.Email);
        }
    }
}
