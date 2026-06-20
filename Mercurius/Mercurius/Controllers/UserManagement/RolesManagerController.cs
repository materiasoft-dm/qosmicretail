using System.Threading;
using Mercurius.Common.Constants;
using Mercurius.Models;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace Mercurius.Controllers.UserManagement
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.ADMIN_ROLES_MANAGEMENT)]
    public class RolesManagerController : BaseController
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUnitOfWork _unitOfWork;

        public RolesManagerController(IHttpContextAccessor httpContextAccessor, RoleManager<IdentityRole> roleManager, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _roleManager = roleManager;
            _unitOfWork = unitOfWork;
        }

        // GET: RolesManager
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below.
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<IdentityRole>());
        }

        // GET: RolesManager/DataTable
        // Server-side endpoint. Role counts are tiny (single digits in practice) so the in-memory
        // page/sort/filter pass is effectively free.
        [HttpGet]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim().ToLowerInvariant();

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            // Only one orderable column (Name); column index is therefore ignored.
            var allRoles = _roleManager.Roles.ToList();
            var recordsTotal = allRoles.Count;

            var filtered = string.IsNullOrEmpty(searchValue)
                ? allRoles
                : allRoles.Where(r => r.Name != null && r.Name.ToLower().Contains(searchValue)).ToList();

            var recordsFiltered = filtered.Count;

            var sorted = sortDir == "desc"
                ? filtered.OrderByDescending(r => r.Name)
                : filtered.OrderBy(r => r.Name);

            var data = sorted.Skip(start).Take(length).Select(r => new
            {
                id = r.Id,
                name = r.Name ?? string.Empty
            }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        // GET: RolesManagerController/Create
        public ActionResult Create()
        {
            var role = new IdentityRole();
            var pages = new List<PageSelectItem>();
            foreach (var x in Common.ModuleRegistry.Modules)
            {
                pages.Add(new PageSelectItem() { IsSelected = false, PageId = x });
            }

            ViewData["pages"] = pages;
            return View(role);
        }

        // POST: RolesManagerController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(IdentityRole role, List<string> pages)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Create the role using RoleManager
                    var result = await _roleManager.CreateAsync(role);
                    if (result.Succeeded)
                    {
                        // Only accept page IDs that exist in the canonical module list.
                        var validModules = new HashSet<string>(Mercurius.Common.ModuleRegistry.Modules, StringComparer.Ordinal);
                        foreach (var page in pages.Where(p => validModules.Contains(p)))
                        {
                            await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(MercuriusClaimTypes.AccessPages, page));
                        }

                        return RedirectToAction(nameof(Index), new { message = $"Successfully created {role.Name} role" });
                    }
                    else
                    {
                        ModelState.AddModelError("", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                return View(role);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating role: {ex.Message}");
                return View(role);
            }
        }

        // GET: RolesManagerController/Edit/5
        public async Task<ActionResult> Edit(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                return NotFound();
            }

            // Get existing claims for this role
            var claims = await _roleManager.GetClaimsAsync(role);
            var existingPages = claims.Where(c => c.Type == MercuriusClaimTypes.AccessPages).Select(c => c.Value).ToList();

            var pages = new List<PageSelectItem>();
            foreach (var x in Common.ModuleRegistry.Modules)
            {
                pages.Add(new PageSelectItem()
                {
                    IsSelected = existingPages.Contains(x),
                    PageId = x
                });
            }

            ViewData["pages"] = pages;
            ViewData["RoleId"] = roleId;
            return View(role);
        }

        // POST: RolesManagerController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string id, List<string> pages)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(id);
                if (role == null)
                {
                    return NotFound();
                }

                // Get and remove existing claims
                var existingClaims = await _roleManager.GetClaimsAsync(role);
                foreach (var claim in existingClaims.Where(c => c.Type == MercuriusClaimTypes.AccessPages))
                {
                    await _roleManager.RemoveClaimAsync(role, claim);
                }

                // Validate submitted page IDs against the canonical module list to prevent
                // arbitrary claim values being injected via a crafted form submission.
                var validModules = new HashSet<string>(Mercurius.Common.ModuleRegistry.Modules, StringComparer.Ordinal);
                foreach (var page in pages.Where(p => validModules.Contains(p)))
                {
                    await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(MercuriusClaimTypes.AccessPages, page));
                }

                return RedirectToAction(nameof(Index), new { message = $"Successfully updated {role.Name}" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating role: {ex.Message}");
                return View();
            }
        }

        // GET: RolesManagerController/Delete/5
        public async Task<ActionResult> Delete(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                return NotFound();
            }
            return View(role);
        }

        // POST: RolesManagerController/Delete/5
        // GET: RolesManagerController/Create
        public ActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var role = new IdentityRole();
            var pages = new List<PageSelectItem>();
            foreach (var x in Common.ModuleRegistry.Modules)
            {
                pages.Add(new PageSelectItem() { IsSelected = false, PageId = x });
            }

            ViewData["pages"] = pages;
            return View(role);
        }

        // POST: RolesManagerController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(IdentityRole role, List<string> pages, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (ModelState.IsValid)
                {
                    // Create the role using RoleManager
                    var result = await _roleManager.CreateAsync(role);
                    if (result.Succeeded)
                    {
                        // Only accept page IDs that exist in the canonical module list.
                        var validModules = new HashSet<string>(Mercurius.Common.ModuleRegistry.Modules, StringComparer.Ordinal);
                        foreach (var page in pages.Where(p => validModules.Contains(p)))
                        {
                            await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(MercuriusClaimTypes.AccessPages, page));
                        }

                        return RedirectToAction(nameof(Index), new { message = $"Successfully created {role.Name} role" });
                    }
                    else
                    {
                        ModelState.AddModelError("", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                return View(role);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating role: {ex.Message}");
                return View(role);
            }
        }

        // GET: RolesManagerController/Edit/5
        public async Task<ActionResult> Edit(string roleId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                return NotFound();
            }

            // Get existing claims for this role
            var claims = await _roleManager.GetClaimsAsync(role);
            var existingPages = claims.Where(c => c.Type == MercuriusClaimTypes.AccessPages).Select(c => c.Value).ToList();

            var pages = new List<PageSelectItem>();
            foreach (var x in Common.ModuleRegistry.Modules)
            {
                pages.Add(new PageSelectItem()
                {
                    IsSelected = existingPages.Contains(x),
                    PageId = x
                });
            }

            ViewData["pages"] = pages;
            ViewData["RoleId"] = roleId;
            return View(role);
        }

        // POST: RolesManagerController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string id, List<string> pages, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var role = await _roleManager.FindByIdAsync(id);
                if (role == null)
                {
                    return NotFound();
                }

                // Get and remove existing claims
                var existingClaims = await _roleManager.GetClaimsAsync(role);
                foreach (var claim in existingClaims.Where(c => c.Type == MercuriusClaimTypes.AccessPages))
                {
                    await _roleManager.RemoveClaimAsync(role, claim);
                }

                // Validate submitted page IDs against the canonical module list to prevent
                // arbitrary claim values being injected via a crafted form submission.
                var validModules = new HashSet<string>(Mercurius.Common.ModuleRegistry.Modules, StringComparer.Ordinal);
                foreach (var page in pages.Where(p => validModules.Contains(p)))
                {
                    await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(MercuriusClaimTypes.AccessPages, page));
                }

                return RedirectToAction(nameof(Index), new { message = $"Successfully updated {role.Name}" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating role: {ex.Message}");
                return View();
            }
        }

        // GET: RolesManagerController/Delete/5
        public async Task<ActionResult> Delete(string roleId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                return NotFound();
            }
            return View(role);
        }

        // POST: RolesManagerController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(string roleId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role == null)
                {
                    return NotFound();
                }

                var result = await _roleManager.DeleteAsync(role);
                if (result.Succeeded)
                {
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    ModelState.AddModelError("", string.Join(", ", result.Errors.Select(e => e.Description)));
                    return View(role);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error deleting role: {ex.Message}");
                return View();
            }
        }
    }
}
