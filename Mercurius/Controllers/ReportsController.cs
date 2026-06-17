using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers
{
    [Authorize]
    public class ReportsController : BaseController
    {
        public ReportsController(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }
    }
}
