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
    [ApiController]
    [Route("api/adjustment-reasons")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CONFIG_ADJUSTMENT_REASONS)]
    public class AdjustmentReasonsApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public AdjustmentReasonsApiController(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        [HttpGet]
        public ActionResult<ListResultDto<AdjustmentReasonDto>> Get(string? search = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var query = _unitOfWork.Query<AdjustmentReason>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(r => (r.Name != null && r.Name.ToLower().Contains(s)) || (r.Description != null && r.Description.ToLower().Contains(s)));
            }
            var items = query.OrderBy(r => r.Name).Select(r => new AdjustmentReasonDto
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                Description = r.Description,
                IsActive = r.IsActive,
                IsInbound = r.IsInbound
            }).ToList();
            return Ok(new ListResultDto<AdjustmentReasonDto> { Items = items, Total = items.Count });
        }

        [HttpPost]
        public async Task<ActionResult<AdjustmentReasonDto>> Create(CreateAdjustmentReasonRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required." });
            var reason = new AdjustmentReason { Name = request.Name, Description = request.Description, IsInbound = request.IsInbound, IsActive = true };
            await _unitOfWork.Repository<AdjustmentReason>().AddAsync(reason, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Ok(new AdjustmentReasonDto { Id = reason.Id, Name = reason.Name, Description = reason.Description, IsActive = reason.IsActive, IsInbound = reason.IsInbound });
        }
    }
}
