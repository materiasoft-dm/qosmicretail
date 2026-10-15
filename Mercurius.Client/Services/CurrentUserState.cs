using Mercurius.Shared.Account;

namespace Mercurius.Client.Services
{
    // The signed-in user's profile for the app shell — who they are, their tenant, the branch
    // they work in and the branches they can switch to. Loaded once by MainLayout (prerendered on
    // the server, then carried into the browser); permission checks use the auth-state claims.
    public class CurrentUserState
    {
        private readonly AccountApiClient _account;

        public CurrentUserState(AccountApiClient account) => _account = account;

        public CurrentUserDto User { get; private set; } = new();

        public event Action? Changed;

        public void Set(CurrentUserDto user)
        {
            User = user;
            Changed?.Invoke();
        }

        // Makes the chosen branch the one this user works in (stock, sales and dashboards follow it).
        public async Task<(bool Success, string? Error)> SwitchBranchAsync(int locationId)
        {
            var (branch, error) = await _account.SwitchBranchAsync(locationId);
            if (branch == null) return (false, error);
            User.CurrentLocationId = branch.Id;
            User.CurrentLocationName = branch.Name;
            Changed?.Invoke();
            return (true, null);
        }
    }
}
