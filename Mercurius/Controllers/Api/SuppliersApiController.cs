using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Mercurius.Shared.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // JSON equivalent of SuppliersController.DataTable — mirrors it exactly, including the
    // active-only filter (SuppliersController.DataTable line ~57: only IsActive suppliers are
    // ever listed; recordsTotal reflects that filtered set, not the whole table).
    [ApiController]
    [Route("api/suppliers")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.CONFIG_SUPPLIERS)]
    public class SuppliersApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public SuppliersApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public ActionResult<SupplierListResultDto> Get(string? search = null, int page = 1, int pageSize = 25, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;
            if (pageSize > 200) pageSize = 200;

            var query = _unitOfWork.Query<Supplier>().Where(sp => sp.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(sp => sp.Name != null && sp.Name.ToLower().Contains(s));
            }

            var total = query.Count();
            var items = query.OrderBy(sp => sp.Name)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(sp => new SupplierListItemDto { Id = sp.Id, Name = sp.Name ?? string.Empty, IsActive = sp.IsActive })
                .ToList();

            return Ok(new SupplierListResultDto { Items = items, Total = total });
        }

        [HttpPost]
        public async Task<ActionResult<SupplierListItemDto>> Create(CreateSupplierRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { error = "Name is required." });
            }

            var supplier = new Supplier { Name = request.Name, IsActive = true };
            await _unitOfWork.Repository<Supplier>().AddAsync(supplier, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Ok(new SupplierListItemDto { Id = supplier.Id, Name = supplier.Name, IsActive = supplier.IsActive });
        }
    }
}
