using System.Threading.Tasks;
using Mercurius.Client.Services;
using Mercurius.Services;
using Mercurius.Shared.Account;
using Microsoft.AspNetCore.Http;

namespace Mercurius.Components
{
    // While a page is prerendered on the server, the app shell's profile is built directly from the
    // database (no HTTP round trip) and MainLayout carries it into the browser with the page.
    public sealed class ServerCurrentUserProfileSource : ICurrentUserProfileSource
    {
        private readonly CurrentUserProfileService _profiles;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ServerCurrentUserProfileSource(CurrentUserProfileService profiles, IHttpContextAccessor httpContextAccessor)
        {
            _profiles = profiles;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<CurrentUserDto> GetAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user == null ? Task.FromResult(new CurrentUserDto()) : _profiles.GetAsync(user);
        }
    }
}
