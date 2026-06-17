using Mercurius.Common.Extensions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Mercurius.Common.Helpers
{
    public class SessionHelper
    {
        [Obsolete("Use httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) directly. " +
                  "Despite the name, this helper already reads from claims, but the call site " +
                  "should be explicit so identity reads stay consistent across controllers.")]
        public static Guid GetLoggedInUserId(HttpContext httpContext)
        {
            if (httpContext == null || httpContext.User == null) return Guid.Empty;

            var userIdValue = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdValue, out var userId) ? userId : Guid.Empty;
        }

    }
}