using System.Net.Http.Json;
using System.Security.Claims;
using Mercurius.Shared.Account;
using Microsoft.AspNetCore.Components.Authorization;

namespace Mercurius.Client.Services
{
    // Builds Blazor's AuthenticationState from api/account/me, which itself just reflects the
    // existing cookie-authenticated ClaimsPrincipal — see AccountApiController and
    // MULTITENANCY_ARCHITECTURE.md's Blazor conversion notes. No token is stored client-side.
    public class CookieAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly HttpClient _http;

        public CookieAuthenticationStateProvider(HttpClient http)
        {
            _http = http;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            const int maxAttempts = 3;

            // This runs once per app boot and its result is cascaded for the whole session — if a
            // single slow/failed request here gets treated as "logged out", App.razor's
            // NotAuthorized handler force-reloads to the login page even though the user's cookie
            // is still perfectly valid. On a slow host (cold shared hosting) that's easy to trip.
            // Retry a couple of times before actually giving up on the user's session.
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var user = await _http.GetFromJsonAsync<CurrentUserDto>("api/account/me");
                    if (user is not { IsAuthenticated: true })
                    {
                        return new AuthenticationState(anonymous);
                    }

                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, user.UserName ?? string.Empty),
                        new(ClaimTypes.Email, user.Email ?? string.Empty)
                    };
                    claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
                    claims.AddRange(user.Pages.Select(p => new Claim("access.pages", p)));

                    var identity = new ClaimsIdentity(claims, authenticationType: "Cookie");
                    return new AuthenticationState(new ClaimsPrincipal(identity));
                }
                catch
                {
                    if (attempt == maxAttempts)
                    {
                        return new AuthenticationState(anonymous);
                    }
                    await Task.Delay(400 * attempt);
                }
            }

            return new AuthenticationState(anonymous);
        }
    }
}
