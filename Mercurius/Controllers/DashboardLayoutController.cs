using System.Threading;
using System.Threading.Tasks;
using Mercurius.Repo.Models;
using Mercurius.Repo.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Mercurius.Services;

namespace Mercurius.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardLayoutController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggerService _logger;

        public DashboardLayoutController(IUnitOfWork unitOfWork, ILoggerService logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDashboardLayout>>> GetLayout(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var layouts = await _unitOfWork.Repository<UserDashboardLayout>().FindAsync(l => l.UserId == userId, ct);
            return Ok(layouts);
        }

        [HttpPost]
        public async Task<IActionResult> SaveLayout([FromBody] List<UserDashboardLayout> layouts, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogInformation("Saving layout for user...");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) 
            {
                _logger.LogWarning("Unauthorized attempt to save layout.");
                return Unauthorized();
            }

            var repo = _unitOfWork.Repository<UserDashboardLayout>();
            
            try 
            {
                // Remove existing layout for user
                var existingLayouts = await repo.FindAsync(l => l.UserId == userId, ct);
                foreach (var layout in existingLayouts)
                {
                    await repo.DeleteAsync(layout.Id, ct);
                }

                // Save new layout
                foreach (var layout in layouts)
                {
                    layout.Id = Guid.NewGuid().ToString();
                    layout.UserId = userId;
                    await repo.AddAsync(layout, ct);
                }

                await _unitOfWork.SaveChangesAsync(ct);
                _logger.LogInformation($"Successfully saved {layouts.Count} widgets for user {userId}");
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save layout", ex);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
