using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Common;
using Mercurius.Shared.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // Mirrors Configurations/LocationsController — a Location always owns a child Address +
    // ContactInformation row (both created here, matching the MVC Create action).
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
            var locations = query.OrderBy(l => l.Name).ToList();
            var contactIds = locations.Select(l => l.ContactInformationId).Distinct().ToList();
            var contactLookup = _unitOfWork.Query<ContactInformation>()
                .Where(c => contactIds.Contains(c.Id))
                .ToDictionary(c => c.Id, c => c.MobilePhoneNumber);

            var items = locations.Select(l => new LocationDto
            {
                Id = l.Id,
                Name = l.Name ?? string.Empty,
                MobilePhone = contactLookup.TryGetValue(l.ContactInformationId, out var m) ? m : null
            }).ToList();
            return Ok(new ListResultDto<LocationDto> { Items = items, Total = items.Count });
        }

        [HttpPost]
        public async Task<ActionResult<LocationDto>> Create(CreateLocationRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required." });

            var address = new Address { IsActive = true };
            await _unitOfWork.Repository<Address>().AddAsync(address, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var contactInformation = new ContactInformation { MobilePhoneNumber = request.MobilePhone };
            await _unitOfWork.Repository<ContactInformation>().AddAsync(contactInformation, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var location = new Location { Name = request.Name, AddressId = address.Id, ContactInformationId = contactInformation.Id };
            await _unitOfWork.Repository<Location>().AddAsync(location, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new LocationDto { Id = location.Id, Name = location.Name, MobilePhone = request.MobilePhone });
        }
    }
}
