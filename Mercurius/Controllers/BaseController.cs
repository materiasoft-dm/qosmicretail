using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading;

namespace Mercurius.Controllers
{
    /// <summary>
    /// Base controller providing common functionality for all controllers.
    /// </summary>
    public abstract class BaseController : Controller
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        protected BaseController(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Gets the cancellation token from the current HTTP request.
        /// This allows callers to propagate request cancellation to repository and service calls.
        /// </summary>
        protected CancellationToken RequestCancellation =>
            _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
    }
}