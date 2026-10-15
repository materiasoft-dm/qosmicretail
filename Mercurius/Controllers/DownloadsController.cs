using System.IO;
using System.Threading.Tasks;
using Mercurius.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers
{
    // The POS app files themselves (the "Download Mobile App" page describes them via
    // api/downloads). Outside wwwroot so only signed-in users can fetch them. Range requests are
    // honored so a large download that drops halfway can resume instead of starting over.
    [Authorize]
    [Route("Downloads")]
    public class DownloadsController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly Mercurius.Services.Auditing.IAuditLogger _auditLogger;

        public DownloadsController(IWebHostEnvironment env, Mercurius.Services.Auditing.IAuditLogger auditLogger)
        {
            _env = env;
            _auditLogger = auditLogger;
        }

        [HttpGet("Windows")]
        public Task<IActionResult> Windows() => Serve(MobileAppDownloads.WindowsZipFileName, "application/zip", "Windows");

        [HttpGet("Android")]
        public Task<IActionResult> Android() => Serve(MobileAppDownloads.AndroidApkFileName, "application/vnd.android.package-archive", "Android");

        private async Task<IActionResult> Serve(string fileName, string contentType, string platform)
        {
            var path = Path.Combine(MobileAppDownloads.Directory(_env), fileName);
            if (!System.IO.File.Exists(path))
            {
                // Not published to this server yet — back to the page that says so.
                return Redirect("~/downloads");
            }

            // One audit row per download, not per resumed chunk.
            if (!Request.Headers.ContainsKey("Range"))
            {
                await _auditLogger.LogAsync(Mercurius.Services.Auditing.AuditActions.MobileAppDownloaded,
                    $"Downloaded the {platform} POS app ({fileName})");
            }

            return PhysicalFile(path, contentType, fileName, enableRangeProcessing: true);
        }
    }
}
