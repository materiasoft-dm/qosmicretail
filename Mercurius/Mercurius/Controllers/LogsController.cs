using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mercurius.Models;

namespace Mercurius.Controllers
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.ADMIN_USERS_MANAGEMENT)]
    public class LogsController : BaseController
    {
        private readonly IWebHostEnvironment _env;

        public LogsController(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment env)
            : base(httpContextAccessor)
        {
            _env = env;
        }

        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var logDirectory = Path.Combine(_env.ContentRootPath, "logs");
            var logFiles = new List<LogFileInfo>();
            if (Directory.Exists(logDirectory))
            {
                foreach (var file in Directory.GetFiles(logDirectory, "mercurius_*.log").OrderByDescending(f => f).Take(30))
                {
                    var fi = new FileInfo(file);
                    logFiles.Add(new LogFileInfo { FileName = Path.GetFileName(file), CreatedDate = fi.CreationTime, Size = fi.Length, LastModified = fi.LastWriteTime });
                }
            }
            return View(logFiles);
        }

        public IActionResult ViewLog(string fileName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var logDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "logs"));
            var fp = Path.GetFullPath(Path.Combine(logDir, fileName));
            if (!fp.StartsWith(logDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !System.IO.File.Exists(fp)) return NotFound();
            var content = System.IO.File.ReadAllText(fp);
            var lines = content.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => new LogLine
            {
                Raw = l, IsError = l.Contains("[ERROR]") || l.Contains("[CRITICAL]"),
                IsWarning = l.Contains("[WARN]"), IsInfo = l.Contains("[INFO]"),
                IsDebug = l.Contains("[DEBUG]") || l.Contains("[TRACE]")
            }).ToList();
            ViewBag.FileName = fileName;
            return View(lines);
        }

        public IActionResult Download(string fileName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var logDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "logs"));
            var fp = Path.GetFullPath(Path.Combine(logDir, fileName));
            if (!fp.StartsWith(logDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !System.IO.File.Exists(fp)) return NotFound();
            return File(System.IO.File.ReadAllBytes(fp), "text/plain", fileName);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Clear(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var logDir = Path.Combine(_env.ContentRootPath, "logs");
            if (Directory.Exists(logDir))
                foreach (var f in Directory.GetFiles(logDir, "mercurius_*.log"))
                    try { System.IO.File.Delete(f); } catch { }
            return RedirectToAction(nameof(Index));
        }
    }
}