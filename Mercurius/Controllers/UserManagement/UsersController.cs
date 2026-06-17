using System.Security.Claims;
using System.Threading;
using Mercurius.Models;
using Mercurius.Repo.IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.UserManagement
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.ADMIN_USERS_MANAGEMENT)]
    public class UsersController : BaseController
    {
        private readonly UserManager<MercuriusUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(IHttpContextAccessor httpContextAccessor, UserManager<MercuriusUser> userManager, RoleManager<IdentityRole> roleManager)
            : base(httpContextAccessor)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: Users
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below.
        public IActionResult Index(bool showDeactivated = false, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            // Surface the toggle to the view so the DataTable ajax can echo it back.
            ViewData["ShowDeactivated"] = showDeactivated;
            return View(Enumerable.Empty<UserUpdateRoleModel>());
        }

        // GET: Users/DataTable
        // Server-side endpoint. Identity is LiteDB-backed (LiteDB.Identity package); the user
        // collection is small enough to materialize, so we filter/sort/page in-memory and
        // resolve roles per page row (page-bounded, capped at 200).
        [HttpGet]
        public async Task<IActionResult> DataTable(int draw = 1, int start = 0, int length = 25, bool showDeactivated = false, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 0;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim().ToLowerInvariant();

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            // Column index → MercuriusUser field. Matches the `columns` array in Index.cshtml.
            // 0 = UserName, 1 = FirstName, 2 = LastName, 3 = Roles (unsortable), 4 = Status, 5 = Actions.
            // Roles column is not sortable here — would require materializing every user's role
            // list to sort by it.

            // Materialize the user set, honoring the deactivated toggle.
            var allUsers = _userManager.Users
                .Where(u => showDeactivated || u.IsActive)
                .ToList();

            var recordsTotal = allUsers.Count;

            // Apply text search across UserName / FirstName / LastName.
            var filtered = string.IsNullOrEmpty(searchValue)
                ? allUsers
                : allUsers.Where(u =>
                        (u.UserName != null && u.UserName.ToLower().Contains(searchValue)) ||
                        (u.FirstName != null && u.FirstName.ToLower().Contains(searchValue)) ||
                        (u.LastName != null && u.LastName.ToLower().Contains(searchValue)))
                    .ToList();

            var recordsFiltered = filtered.Count;

            // Sort.
            IEnumerable<MercuriusUser> sorted = sortColumnIndex switch
            {
                1 => sortDir == "desc"
                    ? filtered.OrderByDescending(u => u.FirstName)
                    : filtered.OrderBy(u => u.FirstName),
                2 => sortDir == "desc"
                    ? filtered.OrderByDescending(u => u.LastName)
                    : filtered.OrderBy(u => u.LastName),
                4 => sortDir == "desc"
                    ? filtered.OrderByDescending(u => u.IsActive)
                    : filtered.OrderBy(u => u.IsActive),
                _ => sortDir == "desc"
                    ? filtered.OrderByDescending(u => u.UserName)
                    : filtered.OrderBy(u => u.UserName)
            };

            var pageItems = sorted.Skip(start).Take(length).ToList();

            // Resolve roles per page row (LiteDB.Identity has no batch user-role join API
            // exposed through UserManager). Bounded by `length` ≤ 200.
            var data = new List<object>(pageItems.Count);
            foreach (var u in pageItems)
            {
                var roles = await _userManager.GetRolesAsync(u);
                data.Add(new
                {
                    id = u.Id,
                    userName = u.UserName ?? string.Empty,
                    firstName = u.FirstName ?? string.Empty,
                    lastName = u.LastName ?? string.Empty,
                    roles = string.Join(", ", roles),
                    isActive = u.IsActive
                });
            }

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        // GET: Users/AssignRoles/{id}
        public async Task<IActionResult> AssignRoles(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = _roleManager.Roles.ToList();

            var model = new UserUpdateRoleModel
            {
                User = user,
                Roles = allRoles
                    .Where(r => userRoles.Contains(r.Name))
                    .ToList()
            };

            ViewData["Roles"] = allRoles;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRoles(string id, List<string> selectedRoles)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            selectedRoles ??= new List<string>();

            // Get current roles
            var currentRoles = await _userManager.GetRolesAsync(user);

            // Remove all current roles
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            // Add selected roles
            foreach (var roleId in selectedRoles)
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role != null)
                {
                    await _userManager.AddToRoleAsync(user, role.Name);
                }
            }

            return RedirectToAction(nameof(Index), new { message = "success" });
        }

        // GET: Users/SetActive/{id}?activate=true|false
        // Confirmation page for toggling MercuriusUser.IsActive.
        public async Task<IActionResult> SetActive(string id, bool activate, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            // Block self-toggle — an admin shouldn't be able to lock themselves out from the
            // user list. (Other admins can still reactivate via the toggled list.)
            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == user.Id)
            {
                TempData["Error"] = "You cannot change your own active status.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["Activate"] = activate;
            return View(user);
        }

        // POST: Users/SetActive/{id}
        [HttpPost, ActionName("SetActive")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActiveConfirmed(string id, bool activate, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == user.Id)
            {
                TempData["Error"] = "You cannot change your own active status.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = activate;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            // After deactivation, hide them from the default list. After activation, show the
            // default (active-only) list so the user can confirm they appear back.
            return RedirectToAction(nameof(Index), new { showDeactivated = !activate });
        }
    }
}
