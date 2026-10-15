using Mercurius.Services;
using Mercurius.Shared.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // What's published for the "Download Mobile App" page. The files themselves are served by
    // DownloadsController (/Downloads/Windows, /Downloads/Android), which audits each download.
    [ApiController]
    [Route("api/downloads")]
    [Authorize]
    public class DownloadsApiController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public DownloadsApiController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet]
        public ActionResult<MobileAppDownloadsDto> Get() => Ok(MobileAppDownloads.Describe(_env));
    }
}
