using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Common.Constants;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Common;
using Mercurius.Shared.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // Branches. A Location always owns a child Address + ContactInformation row (both created
    // here). Its dashboard targets: the monthly one is Location.MetaMonthlyTargetSalesCount, the
    // daily one a "Daily Sales Target" LocationSetting row (DashboardApiController reads both).
    [ApiController]
    [Route("api/locations")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CONFIG_LOCATIONS)]
    public class LocationsApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public LocationsApiController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        [HttpGet]
        public ActionResult<ListResultDto<LocationDto>> Get(string? search = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var query = _unitOfWork.Query<Location>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(l => l.Name != null && l.Name.ToLower().Contains(s));
            }
            var locations = query.OrderByDescending(l => l.IsActive).ThenBy(l => l.Name).ToList();
            var contactIds = locations.Select(l => l.ContactInformationId).Distinct().ToList();
            var contactLookup = _unitOfWork.Query<ContactInformation>()
                .Where(c => contactIds.Contains(c.Id))
                .ToDictionary(c => c.Id);
            var addressIds = locations.Select(l => l.AddressId).Distinct().ToList();
            var addressLookup = _unitOfWork.Query<Address>()
                .Where(a => addressIds.Contains(a.Id))
                .ToDictionary(a => a.Id);
            var dailyTargets = _unitOfWork.Query<LocationSetting>()
                .Where(s => s.SettingCode == LocationSettingKeys.DailySalesTarget)
                .ToList()
                .GroupBy(s => s.LocationId)
                .ToDictionary(g => g.Key, g => int.TryParse(g.First().SettingValue, out var v) && v > 0 ? v : (int?)null);

            var items = locations.Select(l =>
            {
                contactLookup.TryGetValue(l.ContactInformationId, out var contact);
                addressLookup.TryGetValue(l.AddressId, out var address);
                dailyTargets.TryGetValue(l.Id, out var dailyTarget);
                return new LocationDto
                {
                    Id = l.Id,
                    Name = l.Name ?? string.Empty,
                    MobilePhone = contact?.MobilePhoneNumber,
                    PhoneNumber = contact?.PhoneNumber,
                    EmailAddress = contact?.EmailAddress,
                    AddressLine1 = address?.Line1,
                    AddressLine2 = address?.Line2,
                    CityTown = address?.CityTown,
                    Province = address?.Province,
                    Country = address?.Country,
                    MonthlyTargetSalesCount = l.MetaMonthlyTargetSalesCount,
                    DailySalesTarget = dailyTarget,
                    IsActive = l.IsActive,
                    TaxRegisteredName = l.TaxRegisteredName,
                    TaxBusinessName = l.TaxBusinessName,
                    TaxAddress = l.TaxAddress,
                    Tin = l.Tin,
                    TinBranchCode = l.TinBranchCode,
                    IsVatRegistered = l.IsVatRegistered
                };
            }).ToList();
            return Ok(new ListResultDto<LocationDto> { Items = items, Total = items.Count });
        }

        // Updates the location together with the Address and ContactInformation rows it owns.
        // IsActive=false is the delete (soft) — a hard delete fails once any invoice or stock row
        // points at the branch.
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, UpdateLocationRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required." });
            var location = await _unitOfWork.Repository<Location>().GetByIdAsync(id, ct);
            if (location == null) return NotFound();

            location.Name = request.Name.Trim();
            location.MetaMonthlyTargetSalesCount = request.MonthlyTargetSalesCount;
            location.IsActive = request.IsActive;
            ApplyTaxProfile(location, request);
            await _unitOfWork.Repository<Location>().UpdateAsync(location, ct);

            var address = await _unitOfWork.Repository<Address>().GetByIdAsync(location.AddressId, ct);
            if (address != null)
            {
                address.Line1 = request.AddressLine1 ?? string.Empty;
                address.Line2 = request.AddressLine2 ?? string.Empty;
                address.CityTown = request.CityTown ?? string.Empty;
                address.Province = request.Province ?? string.Empty;
                address.Country = request.Country ?? string.Empty;
                address.UpdatedDate = System.DateTime.UtcNow;
                await _unitOfWork.Repository<Address>().UpdateAsync(address, ct);
            }

            var contact = await _unitOfWork.Repository<ContactInformation>().GetByIdAsync(location.ContactInformationId, ct);
            if (contact != null)
            {
                contact.MobilePhoneNumber = request.MobilePhone ?? string.Empty;
                contact.PhoneNumber = request.PhoneNumber ?? string.Empty;
                contact.EmailAddress = request.EmailAddress ?? string.Empty;
                await _unitOfWork.Repository<ContactInformation>().UpdateAsync(contact, ct);
            }

            await SetDailyTargetAsync(location.Id, request.DailySalesTarget, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return NoContent();
        }

        private static void ApplyTaxProfile(Location location, CreateLocationRequest request)
        {
            location.TaxRegisteredName = Clean(request.TaxRegisteredName, 200);
            location.TaxBusinessName = Clean(request.TaxBusinessName, 200);
            location.TaxAddress = Clean(request.TaxAddress, 500);
            location.Tin = Clean(request.Tin, 20);
            location.TinBranchCode = Clean(request.TinBranchCode, 10);
            location.IsVatRegistered = request.IsVatRegistered;
        }

        private static string? Clean(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            return trimmed.Length > max ? trimmed[..max] : trimmed;
        }

        // No target (or zero) removes the setting, so the dashboard falls back to its default.
        private async Task SetDailyTargetAsync(int locationId, int? target, CancellationToken ct)
        {
            var repo = _unitOfWork.Repository<LocationSetting>();
            var existing = (await repo.FindAsync(s => s.LocationId == locationId && s.SettingCode == LocationSettingKeys.DailySalesTarget, ct)).ToList();
            if (target is > 0)
            {
                var setting = existing.FirstOrDefault();
                if (setting == null)
                {
                    await repo.AddAsync(new LocationSetting { LocationId = locationId, SettingCode = LocationSettingKeys.DailySalesTarget, SettingValue = target.Value.ToString() }, ct);
                }
                else if (setting.SettingValue != target.Value.ToString())
                {
                    setting.SettingValue = target.Value.ToString();
                    await repo.UpdateAsync(setting, ct);
                }
                existing = existing.Skip(1).ToList();
            }
            foreach (var extra in existing)
            {
                await repo.DeleteAsync(extra.Id, ct);
            }
        }

        [HttpPost]
        public async Task<ActionResult<LocationDto>> Create(CreateLocationRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required." });

            var address = new Address
            {
                Line1 = request.AddressLine1 ?? string.Empty,
                Line2 = request.AddressLine2 ?? string.Empty,
                CityTown = request.CityTown ?? string.Empty,
                Province = request.Province ?? string.Empty,
                Country = request.Country ?? string.Empty,
                IsActive = true,
                CreatedDate = System.DateTime.UtcNow
            };
            await _unitOfWork.Repository<Address>().AddAsync(address, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var contactInformation = new ContactInformation
            {
                MobilePhoneNumber = request.MobilePhone ?? string.Empty,
                PhoneNumber = request.PhoneNumber ?? string.Empty,
                EmailAddress = request.EmailAddress ?? string.Empty
            };
            await _unitOfWork.Repository<ContactInformation>().AddAsync(contactInformation, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var location = new Location
            {
                Name = request.Name.Trim(),
                MetaMonthlyTargetSalesCount = request.MonthlyTargetSalesCount,
                AddressId = address.Id,
                ContactInformationId = contactInformation.Id,
                IsActive = true
            };
            ApplyTaxProfile(location, request);
            await _unitOfWork.Repository<Location>().AddAsync(location, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await SetDailyTargetAsync(location.Id, request.DailySalesTarget, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new LocationDto
            {
                Id = location.Id,
                Name = location.Name,
                MobilePhone = contactInformation.MobilePhoneNumber,
                PhoneNumber = contactInformation.PhoneNumber,
                EmailAddress = contactInformation.EmailAddress,
                AddressLine1 = address.Line1,
                AddressLine2 = address.Line2,
                CityTown = address.CityTown,
                Province = address.Province,
                Country = address.Country,
                MonthlyTargetSalesCount = location.MetaMonthlyTargetSalesCount,
                DailySalesTarget = request.DailySalesTarget is > 0 ? request.DailySalesTarget : null,
                IsActive = location.IsActive,
                TaxRegisteredName = location.TaxRegisteredName,
                TaxBusinessName = location.TaxBusinessName,
                TaxAddress = location.TaxAddress,
                Tin = location.Tin,
                TinBranchCode = location.TinBranchCode,
                IsVatRegistered = location.IsVatRegistered
            });
        }
    }
}
