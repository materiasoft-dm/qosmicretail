using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.IdentityModel;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Mercurius.ViewComponents
{
    public class HeaderViewComponent : ViewComponent
    {
        private readonly UserManager<MercuriusUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _cache;

        public HeaderViewComponent(UserManager<MercuriusUser> userManager, IUnitOfWork unitOfWork, IMemoryCache cache)
        {
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new HeaderViewModel();

            if (!User.Identity!.IsAuthenticated)
                return View(model);

            var user = await _userManager.GetUserAsync(UserClaimsPrincipal);
            if (user == null)
                return View(model);

            model.User = user;
            
            // Get locations from cache to avoid DB I/O on every page load (M4)
            var cacheKey = "all_locations";
            var locations = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                var allLocations = await _unitOfWork.Repository<Location>().GetAllAsync();
                return allLocations.ToList();
            });

            model.Locations = locations;

            // Get user's current location from cache
            var userLocationCacheKey = $"user_location_{user.Id}";
            var userLocation = await _cache.GetOrCreateAsync(userLocationCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var userLocations = await _unitOfWork.Repository<UserCurrentLocation>().FindAsync(
                    ucl => ucl.UserId == user.Id);
                return userLocations.FirstOrDefault();
            });

            var selectedLocation = userLocation == null 
                ? null 
                : locations.FirstOrDefault(l => l.Id == userLocation.LocationId);

            // If no location found, use first available (M5 - removed write side-effect)
            if (selectedLocation == null)
            {
                selectedLocation = locations.FirstOrDefault();
            }

            model.LocationName = selectedLocation?.Name ?? "No Location Set";
            model.CanCreateInvoice = CheckAccessModule(Common.ModuleRegistry.Pages.NEWSALE_CREATE);
            model.CanManageRoles = UserClaimsPrincipal.HasClaim(MercuriusClaimTypes.AccessPages, Common.ModuleRegistry.Pages.ADMIN_ROLES_MANAGEMENT);
            model.CanManageUsers = UserClaimsPrincipal.HasClaim(MercuriusClaimTypes.AccessPages, Common.ModuleRegistry.Pages.ADMIN_USERS_MANAGEMENT);

            return View(model);
        }

        private bool CheckAccessModule(string module)
        {
            return UserClaimsPrincipal.HasClaim(MercuriusClaimTypes.AccessPages, module);
        }
    }

    public class HeaderViewModel
    {
        public MercuriusUser? User { get; set; }
        public List<Location> Locations { get; set; } = new();
        public string LocationName { get; set; } = "Branch1";
        public bool CanCreateInvoice { get; set; }
        public bool CanManageRoles { get; set; }
        public bool CanManageUsers { get; set; }
    }
}
