using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers
{
    [Authorize]
    public class SettingsController : BaseController
    {
        public SettingsController(IHttpContextAccessor httpContextAccessor)
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
