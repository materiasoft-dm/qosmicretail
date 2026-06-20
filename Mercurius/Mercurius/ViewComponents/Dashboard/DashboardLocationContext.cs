using System.Security.Claims;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.ViewComponents.Dashboard
{
    /// <summary>
    /// Resolves the signed-in user's currently selected branch/location.
    /// Mirrors the lookup HeaderViewComponent does so dashboard widgets stay scoped
    /// to whichever branch the user picked from the location switcher.
    /// </summary>
    internal static class DashboardLocationContext
    {
        public static async Task<int> GetCurrentLocationIdAsync(IUnitOfWork unitOfWork, ClaimsPrincipal user)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return 0;
            }

            var matches = await unitOfWork.Repository<UserCurrentLocation>().FindAsync(c => c.UserId == userId);
            return matches.FirstOrDefault()?.LocationId ?? 0;
        }
    }
}
