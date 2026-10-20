using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Models.Sync;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mercurius.Controllers.Api
{
    // BIR side of mobile sync: a till registers itself as a POS terminal, gets back what it needs
    // to print BIR invoices (its MIN/PTU, the branch's TIN and registered name, the software's
    // accreditation), and uploads its Z-readings. Invoices themselves still go up through
    // SyncController.PushInvoices, which now also stores their BIR fields.
    //
    // The till is the authority for its own counters (invoice serial, Z counter, grand total):
    // it issues them offline. The server keeps the highest values it has seen so a re-registered
    // terminal carries on from them instead of restarting at 1.
    [ApiController]
    [Route("api/sync")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BirSyncController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly Mercurius.Services.UserPermissions _userPermissions;
        private readonly Mercurius.Services.BirSupplierOptions _supplier;
        private HashSet<string>? _permissions;

        public BirSyncController(IUnitOfWork unitOfWork, Mercurius.Services.UserPermissions userPermissions,
            IOptions<Mercurius.Services.BirSupplierOptions> supplier)
        {
            _unitOfWork = unitOfWork;
            _userPermissions = userPermissions;
            _supplier = supplier.Value;
        }

        private Guid CurrentUserId =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;

        private async Task<bool> CanAsync(string permission)
        {
            _permissions ??= await _userPermissions.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));
            return _permissions.Contains(permission);
        }

        // Idempotent: called on every sync pass, so a renamed terminal or a new branch assignment
        // reaches the server, and a branch's changed TIN or name reaches the till.
        [HttpPost("terminal/register")]
        public async Task<ActionResult<TerminalConfigDto>> RegisterTerminal([FromBody] TerminalRegisterRequest request, CancellationToken ct = default)
        {
            var deviceId = (request.DeviceId ?? "").Trim();
            if (deviceId.Length == 0)
            {
                deviceId = Request.Headers[Mercurius.Services.Auditing.HttpAuditContextProvider.DeviceIdHeader].ToString().Trim();
            }
            if (deviceId.Length == 0 || deviceId.Length > 100) return BadRequest(new { error = "A device id is required." });

            int? locationId = request.LocationId > 0 && await _unitOfWork.Query<Location>().AnyAsync(l => l.Id == request.LocationId, ct)
                ? request.LocationId
                : null;
            var name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
            if (name?.Length > 200) name = name[..200];

            var terminal = await _unitOfWork.Query<PosTerminal>().FirstOrDefaultAsync(t => t.DeviceId == deviceId, ct);
            if (terminal == null)
            {
                terminal = new PosTerminal
                {
                    DeviceId = deviceId,
                    Name = name,
                    LocationId = locationId,
                    CreatedDate = DateTime.UtcNow
                };
                await _unitOfWork.Repository<PosTerminal>().AddAsync(terminal, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            else if (terminal.Name != name || (locationId.HasValue && terminal.LocationId != locationId))
            {
                terminal.Name = name;
                // A device with no branch assigned keeps the last one it had.
                terminal.LocationId = locationId ?? terminal.LocationId;
                terminal.UpdatedDate = DateTime.UtcNow;
                await _unitOfWork.Repository<PosTerminal>().UpdateAsync(terminal, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return Ok(await ToConfigAsync(terminal, ct));
        }

        // The MIN, serial number and Permit to Use come from BIR per machine, so they're typed in
        // on the till itself, by someone allowed to (an admin, by default).
        [HttpPut("terminal/registration")]
        public async Task<ActionResult<TerminalConfigDto>> UpdateRegistration([FromBody] TerminalRegistrationUpdate request, CancellationToken ct = default)
        {
            if (!await CanAsync(Mercurius.Common.ModuleRegistry.Pages.CONFIG_POS_TERMINALS))
            {
                return StatusCode(403, new { error = "This account isn't allowed to change a terminal's BIR registration." });
            }
            var terminal = await _unitOfWork.Query<PosTerminal>().FirstOrDefaultAsync(t => t.Id == request.TerminalId, ct);
            if (terminal == null) return NotFound(new { error = "This terminal isn't registered. Sync, then try again." });

            terminal.MachineIdentificationNumber = Clean(request.MachineIdentificationNumber, 50);
            terminal.SerialNumber = Clean(request.SerialNumber, 100);
            terminal.PermitToUseNumber = Clean(request.PermitToUseNumber, 100);
            terminal.PermitToUseDateIssued = request.PermitToUseDateIssued?.Date;
            terminal.UpdatedDate = DateTime.UtcNow;
            await _unitOfWork.Repository<PosTerminal>().UpdateAsync(terminal, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(await ToConfigAsync(terminal, ct));
        }

        // Create-once by SyncId, like invoices.
        // The tenant's active discount types, for the Sell page. Small, so it's always the full
        // list: the device replaces its cached copy each time.
        [HttpGet("discount-types")]
        public async Task<ActionResult<List<Mercurius.Shared.Configuration.DiscountTypeDto>>> GetDiscountTypes(CancellationToken ct = default)
        {
            var items = await _unitOfWork.Query<DiscountType>().Where(d => d.IsActive)
                .OrderBy(d => d.Category == 0).ThenBy(d => d.Category).ThenBy(d => d.Name)
                .ToListAsync(ct);
            return Ok(items.Select(DiscountTypesApiController.ToDto).ToList());
        }

        [HttpPost("zreadings/push")]
        public async Task<IActionResult> PushZReadings([FromBody] List<ZReadingPushDto> items, CancellationToken ct = default)
        {
            var results = new List<SyncPushResultItem>();
            var canSell = await CanAsync(Mercurius.Common.ModuleRegistry.Pages.NEWSALE_CREATE);

            foreach (var item in items ?? new List<ZReadingPushDto>())
            {
                try
                {
                    var existing = await _unitOfWork.Query<PosZReading>().FirstOrDefaultAsync(z => z.SyncId == item.SyncId, ct);
                    if (existing != null)
                    {
                        results.Add(new SyncPushResultItem { SyncId = item.SyncId, Created = false, LastModifiedUtc = existing.CreatedDate });
                        continue;
                    }
                    if (!canSell)
                    {
                        results.Add(new SyncPushResultItem { SyncId = item.SyncId, Error = "This account isn't allowed to make sales." });
                        continue;
                    }
                    var terminal = await _unitOfWork.Query<PosTerminal>().FirstOrDefaultAsync(t => t.Id == item.PosTerminalId, ct);
                    if (terminal == null)
                    {
                        results.Add(new SyncPushResultItem { SyncId = item.SyncId, Error = "This terminal isn't registered on the server." });
                        continue;
                    }

                    var now = DateTime.UtcNow;
                    await _unitOfWork.Repository<PosZReading>().AddAsync(new PosZReading
                    {
                        SyncId = item.SyncId,
                        PosTerminalId = terminal.Id,
                        LocationId = terminal.LocationId,
                        ZCounter = item.ZCounter,
                        ResetCounter = item.ResetCounter,
                        ReadingDate = item.ReadingDate,
                        CreatedDate = now,
                        CreatedBy = CurrentUserId,
                        SalesCount = item.SalesCount,
                        BeginningInvoiceSequence = item.BeginningInvoiceSequence,
                        EndingInvoiceSequence = item.EndingInvoiceSequence,
                        GrossSales = item.GrossSales,
                        LessVat = item.LessVat,
                        DiscountTotal = item.DiscountTotal,
                        SeniorCitizenDiscount = item.SeniorCitizenDiscount,
                        PwdDiscount = item.PwdDiscount,
                        SoloParentDiscount = item.SoloParentDiscount,
                        NationalAthleteDiscount = item.NationalAthleteDiscount,
                        MedalOfValorDiscount = item.MedalOfValorDiscount,
                        NetSales = item.NetSales,
                        VatableSales = item.VatableSales,
                        VatAmount = item.VatAmount,
                        VatExemptSales = item.VatExemptSales,
                        ZeroRatedSales = item.ZeroRatedSales,
                        CashSales = item.CashSales,
                        CardSales = item.CardSales,
                        PreviousGrandTotal = item.PreviousGrandTotal,
                        NewGrandTotal = item.NewGrandTotal,
                        ReportText = item.ReportText
                    }, ct);

                    if (item.ZCounter >= terminal.LastZCounter)
                    {
                        terminal.LastZCounter = item.ZCounter;
                        terminal.AccumulatedGrandTotal = item.NewGrandTotal;
                        terminal.ResetCounter = Math.Max(terminal.ResetCounter, item.ResetCounter);
                        terminal.LastInvoiceSequence = Math.Max(terminal.LastInvoiceSequence, item.EndingInvoiceSequence);
                        terminal.UpdatedDate = now;
                        await _unitOfWork.Repository<PosTerminal>().UpdateAsync(terminal, ct);
                    }
                    await _unitOfWork.SaveChangesAsync(ct);
                    results.Add(new SyncPushResultItem { SyncId = item.SyncId, Created = true, LastModifiedUtc = now });
                }
                catch (Exception ex)
                {
                    results.Add(new SyncPushResultItem { SyncId = item.SyncId, Error = ex.Message });
                }
            }
            return Ok(results);
        }

        private async Task<TerminalConfigDto> ToConfigAsync(PosTerminal terminal, CancellationToken ct)
        {
            Location? location = null;
            Address? address = null;
            if (terminal.LocationId is int locationId)
            {
                location = await _unitOfWork.Query<Location>().AsNoTracking().FirstOrDefaultAsync(l => l.Id == locationId, ct);
                if (location != null)
                {
                    address = await _unitOfWork.Query<Address>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == location.AddressId, ct);
                }
            }

            return new TerminalConfigDto
            {
                TerminalId = terminal.Id,
                TerminalCode = PosTerminal.CodeFor(terminal.Id),
                Name = terminal.Name,
                MachineIdentificationNumber = terminal.MachineIdentificationNumber,
                SerialNumber = terminal.SerialNumber,
                PermitToUseNumber = terminal.PermitToUseNumber,
                PermitToUseDateIssued = terminal.PermitToUseDateIssued,
                LastInvoiceSequence = terminal.LastInvoiceSequence,
                LastZCounter = terminal.LastZCounter,
                AccumulatedGrandTotal = terminal.AccumulatedGrandTotal,
                ResetCounter = terminal.ResetCounter,
                LocationId = terminal.LocationId,
                SellerRegisteredName = location?.TaxRegisteredName,
                SellerBusinessName = string.IsNullOrWhiteSpace(location?.TaxBusinessName) ? location?.Name : location.TaxBusinessName,
                SellerAddress = string.IsNullOrWhiteSpace(location?.TaxAddress) ? FormatAddress(address) : location.TaxAddress,
                SellerTin = location?.Tin,
                SellerBranchCode = location?.TinBranchCode,
                SellerIsVatRegistered = location?.IsVatRegistered ?? true,
                SupplierName = Blank(_supplier.Name),
                SupplierAddress = Blank(_supplier.Address),
                SupplierTin = Blank(_supplier.Tin),
                AccreditationNumber = Blank(_supplier.AccreditationNumber),
                AccreditationDateIssued = _supplier.AccreditationDateIssued,
                AccreditationValidUntil = _supplier.AccreditationValidUntil,
                CanEditRegistration = await CanAsync(Mercurius.Common.ModuleRegistry.Pages.CONFIG_POS_TERMINALS)
            };
        }

        private static string? FormatAddress(Address? a)
        {
            if (a == null) return null;
            var parts = new[] { a.Line1, a.Line2, a.CityTown, a.Province }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim());
            var text = string.Join(", ", parts);
            return text.Length == 0 ? null : text;
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? Clean(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            return trimmed.Length > max ? trimmed[..max] : trimmed;
        }
    }
}
