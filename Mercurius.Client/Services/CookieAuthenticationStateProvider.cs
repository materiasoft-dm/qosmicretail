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
                // Network hiccup or the API being briefly unreachable shouldn't crash the whole
                // app shell — fall back to "not signed in" and let the user retry.
                return new AuthenticationState(anonymous);
            }
        }
    }
}
