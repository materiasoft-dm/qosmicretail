using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Bir;
using Mercurius.Shared.Common;
using Mercurius.Shared.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // Settings → Discount Types: the discounts a cashier can apply to a ticket (synced to the
    // mobile app by BirSyncController). Every type, active or not; "deleting" is IsActive = false,
    // since invoices keep pointing at the type they used.
    [ApiController]
    [Route("api/discount-types")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CONFIG_DISCOUNT_TYPES)]
    public class DiscountTypesApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public DiscountTypesApiController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        [HttpGet]
        public ActionResult<ListResultDto<DiscountTypeDto>> Get(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var items = _unitOfWork.Query<DiscountType>()
                .OrderBy(d => d.Category == 0).ThenBy(d => d.Category).ThenBy(d => d.Name)
                .AsEnumerable().Select(ToDto).ToList();
            return Ok(new ListResultDto<DiscountTypeDto> { Items = items, Total = items.Count });
        }

        [HttpPost]
        public async Task<ActionResult<DiscountTypeDto>> Create(DiscountTypeDto request, CancellationToken ct = default)
        {
            var error = Validate(request);
            if (error != null) return BadRequest(new { error });

            var entity = new DiscountType { CreatedDate = DateTime.UtcNow };
            Apply(entity, request);
            entity.IsActive = true;
            await _unitOfWork.Repository<DiscountType>().AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(ToDto(entity));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, DiscountTypeDto request, CancellationToken ct = default)
        {
            var entity = await _unitOfWork.Repository<DiscountType>().GetByIdAsync(id, ct);
            if (entity == null) return NotFound();
            var error = Validate(request);
            if (error != null) return BadRequest(new { error });

            Apply(entity, request);
            entity.IsActive = request.IsActive;
            await _unitOfWork.Repository<DiscountType>().UpdateAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok();
        }

        internal static string? Validate(DiscountTypeDto r)
        {
            if (string.IsNullOrWhiteSpace(r.Name)) return "Name is required.";
            if (r.Name.Trim().Length > 100) return "Name must be 100 characters or fewer.";
            if (r.ShortLabel?.Trim().Length > 10) return "Receipt code must be 10 characters or fewer.";
            if (r.IdLabel?.Trim().Length > 50) return "ID label must be 50 characters or fewer.";
            if (!Enum.IsDefined(typeof(DiscountValueKind), r.ValueKind)) return "Choose percent or fixed amount.";
            if (!Enum.IsDefined(typeof(SpecialDiscountType), r.Category)) return "Unknown BIR category.";
            if (r.Value <= 0) return "Value must be more than zero.";
            if (r.ValueKind == (int)DiscountValueKind.Percent && r.Value > 100) return "A percent discount can't be more than 100%.";
            return null;
        }

        private static void Apply(DiscountType entity, DiscountTypeDto r)
        {
            entity.Name = r.Name.Trim();
            entity.ShortLabel = string.IsNullOrWhiteSpace(r.ShortLabel) ? null : r.ShortLabel.Trim();
            entity.ValueKind = r.ValueKind;
            entity.Value = Math.Round(r.Value, 2);
            entity.IsVatExempt = r.IsVatExempt;
            entity.RequiresCustomerId = r.RequiresCustomerId;
            entity.IdLabel = string.IsNullOrWhiteSpace(r.IdLabel) ? null : r.IdLabel.Trim();
            entity.Category = r.Category;
            entity.UpdatedDate = DateTime.UtcNow;
        }

        internal static DiscountTypeDto ToDto(DiscountType d) => new()
        {
            Id = d.Id,
            Name = d.Name,
            ShortLabel = d.ShortLabel,
            ValueKind = d.ValueKind,
            Value = d.Value,
            IsVatExempt = d.IsVatExempt,
            RequiresCustomerId = d.RequiresCustomerId,
            IdLabel = d.IdLabel,
            Category = d.Category,
            IsActive = d.IsActive
        };
    }
}
