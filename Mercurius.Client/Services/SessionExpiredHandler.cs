using System.Net;
using Microsoft.AspNetCore.Components;

namespace Mercurius.Client.Services
{
    // A 401 doesn't always mean the session ended. POST /api/admin/data-query answers 401 for a
    // wrong *admin key* (that key is checked on top of the permission), and a user trying a key
    // must be able to retry — being bounced to the sign-in page mid-form loses the SQL they typed.
    // Those calls opt out of SessionExpiredHandler's redirect with this flag.
    public static class ApiRequestOptions
    {
        public static readonly HttpRequestOptionsKey<bool> Suppress401Redirect = new("Suppress401Redirect");
    }

    // The server answers API calls with 401 once the sign-in cookie has expired (it never
    // redirects /api requests to the sign-in page). When that happens with a page open, send the
    // browser to sign in again and come back to the same page afterwards.
    public sealed class SessionExpiredHandler : DelegatingHandler
    {
        private readonly NavigationManager _navigation;

        public SessionExpiredHandler(NavigationManager navigation) => _navigation = navigation;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && !IsSuppressed(request))
            {
                var returnUrl = "/" + _navigation.ToBaseRelativePath(_navigation.Uri);
                _navigation.NavigateTo($"account/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
            }
            return response;
        }

        private static bool IsSuppressed(HttpRequestMessage request) =>
            request.Options.TryGetValue(ApiRequestOptions.Suppress401Redirect, out var suppressed) && suppressed;
    }
}
