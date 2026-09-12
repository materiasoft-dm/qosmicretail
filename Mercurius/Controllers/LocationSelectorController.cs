using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using System.Security.Claims;

namespace Mercurius.Controllers
{
    [Authorize]
    public class LocationSelectorController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public LocationSelectorController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> SetLocation(int id, string returnUrl, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            // The target Location must belong to the caller's own tenant. Query<T>() is already
            // filtered by MercuriusDbContext's global tenant query filter, so a Location from a
            // different tenant simply doesn't exist as far as this lookup is concerned — this
            // isn't "extra" tenant-checking logic, it's just not bypassing the filter the way the
            // old blind upsert (below) effectively did. Confirmed via a real cross-tenant test:
            // before this check, a tenant's admin could switch UserCurrentLocation to point at
            // another tenant's Location id just by guessing/incrementing it.
            var targetLocation = _unitOfWork.Query<Location>().FirstOrDefault(l => l.Id == id);
            if (targetLocation == null)
            {
                return Forbid();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var userLocationRepo = _unitOfWork.Repository<UserCurrentLocation>();
            var userLocations = await userLocationRepo.FindAsync(c => c.UserId == userId, ct);
            var userLocation = userLocations.FirstOrDefault();

            if (userLocation == null)
            {
                userLocation = new UserCurrentLocation { UserId = userId, LocationId = id };
                await userLocationRepo.AddAsync(userLocation, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            else if (id != userLocation.LocationId)
            {
                userLocation.LocationId = id;
                await userLocationRepo.UpdateAsync(userLocation, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            var safeReturn = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : Url.Action("Index", "Home") ?? "/";
            var separator = safeReturn.Contains('?') ? '&' : '?';
            return Redirect($"{safeReturn}{separator}message=successfully switched store");
        }
    }
}
