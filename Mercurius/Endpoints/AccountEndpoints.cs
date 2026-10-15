using System.Security.Claims;
using Mercurius.Repo.IdentityModel;
using Mercurius.Services.Auditing;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace Mercurius.Endpoints
{
    public static class AccountEndpoints
    {
        // Sign-out is a form POST with an antiforgery token (validated by UseAntiforgery), so a
        // cross-site link or form can't sign anyone out. Audited while the user is still known.
        public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/account/logout", async (ClaimsPrincipal user, SignInManager<MercuriusUser> signInManager, IAuditLogger auditLogger) =>
            {
                if (user.Identity?.IsAuthenticated == true)
                {
                    await auditLogger.LogAsync(AuditActions.Logout, $"Signed out of the web app: {user.Identity.Name}");
                }
                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect("~/account/login");
            })
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

            return endpoints;
        }
    }
}
