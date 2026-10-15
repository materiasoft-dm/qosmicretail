using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Services
{
    /// <summary>
    /// The branch (Location) the signed-in user currently works in — set with the branch switcher
    /// (UserCurrentLocation). Sales, stock adjustments, shipments and dashboard figures are recorded
    /// against / scoped to it. 0 when the user has never picked one.
    /// </summary>
    internal static class CurrentLocation
    {
        public static async Task<int> GetIdAsync(IUnitOfWork unitOfWork, ClaimsPrincipal user)
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
