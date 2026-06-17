using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers
{
    /// <summary>
    /// Handles /quickinvoice route - redirects to the New Sale page.
    /// </summary>
    [Authorize(Policy = Common.ModuleRegistry.Pages.NEWSALE_CREATE)]
    [Route("quickinvoice")]
    public class QuickInvoiceController : BaseController
    {
        public QuickInvoiceController(IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
        }

        [HttpGet]
        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return RedirectToAction("NewSale", "Sales");
        }
    }
}
