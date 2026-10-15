using System.Security.Claims;
using Mercurius.Common.Constants;

namespace Mercurius.Client
{
    // UI-side permission checks against the signed-in user's claims (the same claims and names the
    // server's policies use). They only decide what to show; the server authorizes every API call.
    public static class Access
    {
        public static bool Can(this ClaimsPrincipal user, string page) =>
            user.HasClaim(MercuriusClaimTypes.AccessPages, page);

        public static bool CanAny(this ClaimsPrincipal user, params string[] pages) => pages.Any(user.Can);

        public static bool IsPlatformAdmin(this ClaimsPrincipal user) =>
            user.HasClaim(MercuriusClaimTypes.IsPlatformAdmin, "true");
    }
}
