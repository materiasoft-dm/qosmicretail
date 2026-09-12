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
    // JSON equivalent of RefundReasonsController — including every reason (active or archived),
    // matching that controller's own DataTable. Archiving is the same soft-delete-via-Edit
    // pattern as the MVC version (no hard-delete action exists there either).
    [ApiController]
    [Route("api/refund-reasons")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CONFIG_REFUND_REASONS)]
    public class RefundReasonsApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public RefundReasonsApiController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        [HttpGet]
        public ActionResult<ListResultDto<RefundReasonDto>> Get(string? search = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var query = _unitOfWork.Query<RefundReason>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(r => (r.Name != null && r.Name.ToLower().Contains(s)) || (r.Description != null && r.Description.ToLower().Contains(s)));
            }
            var items = query.OrderBy(r => r.Name).Select(r => new RefundReasonDto
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                Description = r.Description,
                IsActive = r.IsActive
            }).ToList();
            return Ok(new ListResultDto<RefundReasonDto> { Items = items, Total = items.Count });
        }

        [HttpPost]
        public async Task<ActionResult<RefundReasonDto>> Create(CreateRefundReasonRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required." });
            var reason = new RefundReason { Name = request.Name, Description = request.Description, IsActive = true };
            await _unitOfWork.Repository<RefundReason>().AddAsync(reason, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(new RefundReasonDto { Id = reason.Id, Name = reason.Name, Description = reason.Description, IsActive = reason.IsActive });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateRefundReasonRequest request, CancellationToken ct = default)
        {
            var reason = await _unitOfWork.Repository<RefundReason>().GetByIdAsync(id, ct);
            if (reason == null) return NotFound();
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required." });

            reason.Name = request.Name;
            reason.Description = request.Description;
            reason.IsActive = request.IsActive;
            await _unitOfWork.Repository<RefundReason>().UpdateAsync(reason, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok();
        }
    }
}
