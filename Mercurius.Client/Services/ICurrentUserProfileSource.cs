using System.Net.Http.Json;
using Mercurius.Shared.Account;

namespace Mercurius.Client.Services
{
    // Where the app shell gets the signed-in user's profile (tenant, branch, branches). The server
    // builds it directly while prerendering; in the browser it comes from api/account/me — though
    // normally only after a branch switch, since MainLayout carries the prerendered copy over.
    public interface ICurrentUserProfileSource
    {
        Task<CurrentUserDto> GetAsync();
    }

    public sealed class HttpCurrentUserProfileSource : ICurrentUserProfileSource
    {
        private readonly HttpClient _http;

        public HttpCurrentUserProfileSource(HttpClient http) => _http = http;

        public async Task<CurrentUserDto> GetAsync() =>
            await _http.GetFromJsonAsync<CurrentUserDto>("api/account/me") ?? new CurrentUserDto();
    }
}
