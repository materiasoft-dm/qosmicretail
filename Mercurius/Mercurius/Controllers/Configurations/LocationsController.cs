using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Mercurius.Common.Helpers;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;

namespace Mercurius.Controllers.Configurations
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.CONFIG_LOCATIONS)]
    public class LocationsController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;

        public LocationsController(IHttpContextAccessor httpContextAccessor, IUnitOfWork unitOfWork)
            : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        // GET: Locations
        // Renders the page shell only — the table is populated by AJAX calls to
        // the DataTable() action below.
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(Enumerable.Empty<Location>());
        }

        // GET: Locations/DataTable
        // Server-side endpoint. ContactInformation.MobilePhoneNumber is batch-resolved
        // (avoids N+1 from the old per-row HydrateLocationAsync).
        [HttpGet]
        public IActionResult DataTable(int draw = 1, int start = 0, int length = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var q = Request.Query;
            var sortColumnIndex = int.TryParse(q["order[0][column]"], out var sci) ? sci : 0;
            var sortDir = (string?)q["order[0][dir]"] == "desc" ? "desc" : "asc";
            var searchValue = ((string?)q["search[value]"] ?? string.Empty).Trim();

            // Column index → Location field. Matches the `columns` array in Index.cshtml.
            // 0 = Name, 1 = Mobile (sort by ContactInformationId — joined sort would require
            //                       fetching the full contact list), 2 = Actions.
            string sortField = sortColumnIndex switch
            {
                0 => nameof(Location.Name),
                1 => nameof(Location.ContactInformationId),
                _ => nameof(Location.Name)
            };

            if (length < 1) length = 25;
            if (length > 200) length = 200;

            var collection = _unitOfWork.GetCollection<Location>();

            var recordsTotal = collection.Count();

            var query = collection.Query();
            if (!string.IsNullOrEmpty(searchValue))
            {
                var s = searchValue.ToLowerInvariant();
                query = query.Where(l => l.Name != null && l.Name.ToLower().Contains(s));
            }

            var recordsFiltered = query.Count();

            var bsonField = LiteDB.BsonExpression.Create($"$.{sortField}");
            query = sortDir == "desc"
                ? query.OrderByDescending(bsonField)
                : query.OrderBy(bsonField);

            var pageItems = query.Skip(start).Limit(length).ToList();

            // Batch-resolve ContactInformation phone numbers (1 query, no N+1).
            var contactIds = pageItems
                .Where(l => l.ContactInformationId > 0)
                .Select(l => l.ContactInformationId)
                .Distinct()
                .ToList();
            var contactLookup = contactIds.Count == 0
                ? new Dictionary<int, string>()
                : _unitOfWork.GetCollection<ContactInformation>()
                    .Find(c => contactIds.Contains(c.Id))
                    .ToDictionary(c => c.Id, c => c.MobilePhoneNumber ?? string.Empty);

            var data = pageItems.Select(l =>
            {
                string mobile = string.Empty;
                if (l.ContactInformationId > 0)
                {
                    contactLookup.TryGetValue(l.ContactInformationId, out mobile!);
                }
                return new
                {
                    id = l.Id,
                    name = l.Name ?? string.Empty,
                    mobilePhone = mobile ?? string.Empty
                };
            }).ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        public async Task<IActionResult> Details(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null)
            {
                return NotFound();
            }

            var location = await GetHydratedLocationAsync(id.Value, ct);
            if (location == null)
            {
                return NotFound();
            }

            return View(location);
        }

        public IActionResult Create(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View(new Location
            {
                Address = new Address(),
                ContactInformation = new ContactInformation()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Location location, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (ModelState.IsValid)
            {
                location.Address ??= new Address();
                location.ContactInformation ??= new ContactInformation();

                location.Address.CreatedDate = DateTimeHelper.GetLocalizedDate();
                location.Address.CreatedBy = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)
                    ? uid
                    : Guid.Empty;

                await _unitOfWork.Repository<Address>().AddAsync(location.Address, ct);
                await _unitOfWork.Repository<ContactInformation>().AddAsync(location.ContactInformation, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                location.AddressId = location.Address.Id;
                location.ContactInformationId = location.ContactInformation.Id;
                await _unitOfWork.Repository<Location>().AddAsync(location, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                return RedirectToAction(nameof(Index));
            }

            return View(location);
        }

        public async Task<IActionResult> Edit(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null)
            {
                return NotFound();
            }

            var location = await GetHydratedLocationAsync(id.Value, ct);
            if (location == null)
            {
                return NotFound();
            }

            return View(location);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Location location, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id != location.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                if (!await LocationExistsAsync(location.Id, ct))
                {
                    return NotFound();
                }

                location.Address ??= new Address();
                location.ContactInformation ??= new ContactInformation();

                if (location.Address.Id > 0)
                {
                    location.AddressId = location.Address.Id;
                    location.Address.UpdatedDate = DateTimeHelper.GetLocalizedDate();
                    location.Address.UpdatedBy = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)
                        ? uid
                        : null;
                    await _unitOfWork.Repository<Address>().UpdateAsync(location.Address, ct);
                }
                else
                {
                    await _unitOfWork.Repository<Address>().AddAsync(location.Address, ct);
                    location.AddressId = location.Address.Id;
                }

                if (location.ContactInformation.Id > 0)
                {
                    location.ContactInformationId = location.ContactInformation.Id;
                    await _unitOfWork.Repository<ContactInformation>().UpdateAsync(location.ContactInformation, ct);
                }
                else
                {
                    await _unitOfWork.Repository<ContactInformation>().AddAsync(location.ContactInformation, ct);
                    location.ContactInformationId = location.ContactInformation.Id;
                }

                await _unitOfWork.Repository<Location>().UpdateAsync(location, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                return RedirectToAction(nameof(Index));
            }

            return View(location);
        }

        public async Task<IActionResult> Delete(int? id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null)
            {
                return NotFound();
            }

            var location = await GetHydratedLocationAsync(id.Value, ct);
            if (location == null)
            {
                return NotFound();
            }

            return View(location);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (await LocationExistsAsync(id, ct))
            {
                await _unitOfWork.Repository<Location>().DeleteAsync(id, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<Location?> GetHydratedLocationAsync(int id, CancellationToken ct = default)
        {
            var location = await _unitOfWork.Repository<Location>().GetByIdAsync(id, ct);
            if (location == null)
            {
                return null;
            }

            await HydrateLocationAsync(location, ct);
            return location;
        }

        private async Task HydrateLocationAsync(Location location, CancellationToken ct = default)
        {
            if (location.Address == null && location.AddressId > 0)
            {
                location.Address = await _unitOfWork.Repository<Address>().GetByIdAsync(location.AddressId, ct);
            }

            if (location.ContactInformation == null && location.ContactInformationId > 0)
            {
                location.ContactInformation = await _unitOfWork.Repository<ContactInformation>().GetByIdAsync(location.ContactInformationId, ct);
            }

            location.Address ??= new Address();
            location.ContactInformation ??= new ContactInformation();
        }

        private async Task<bool> LocationExistsAsync(int id, CancellationToken ct = default)
        {
            return await _unitOfWork.Repository<Location>().ExistsAsync(id, ct);
        }
    }
}
