using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mercurius.Shared.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Mercurius.Controllers.Api
{
    // JSON equivalent of LogsController — a Blazor WASM client can't do file I/O itself, so this
    // wraps the same file operations server-side, including the identical path-traversal guard
    // (resolve full path, verify it's still under the logs directory before touching it).
    [ApiController]
    [Route("api/logs")]
    [Authorize(Policy = Mercurius.Common.ModuleRegistry.Pages.ADMIN_USERS_MANAGEMENT)]
    public class LogsApiController : ControllerBase
    {
        private readonly string _logDir;
        public LogsApiController(IWebHostEnvironment env)
        {
            _logDir = Path.Combine(env.ContentRootPath, "logs");
        }

        [HttpGet]
        public ActionResult<List<LogFileDto>> Get()
        {
            if (!Directory.Exists(_logDir)) return Ok(new List<LogFileDto>());
            var files = Directory.GetFiles(_logDir, "mercurius_*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.Name)
                .Take(30)
                .Select(f => new LogFileDto { FileName = f.Name, LastModified = f.LastWriteTimeUtc, Size = f.Length })
                .ToList();
            return Ok(files);
        }

        [HttpGet("{fileName}")]
        public ActionResult<List<LogLineDto>> GetLines(string fileName)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_logDir, fileName));
            if (!fullPath.StartsWith(Path.GetFullPath(_logDir) + Path.DirectorySeparatorChar) || !System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var lines = System.IO.File.ReadAllLines(fullPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => new LogLineDto
                {
                    Text = l,
                    IsError = l.Contains("[ERROR]") || l.Contains("[CRITICAL]"),
                    IsWarning = l.Contains("[WARN]"),
                    IsInfo = l.Contains("[INFO]"),
                    IsDebug = l.Contains("[DEBUG]") || l.Contains("[TRACE]")
                }).ToList();
            return Ok(lines);
        }

        [HttpDelete]
        public IActionResult Clear()
        {
            if (!Directory.Exists(_logDir)) return Ok();
            foreach (var f in Directory.GetFiles(_logDir, "mercurius_*.log"))
            {
                try { System.IO.File.Delete(f); } catch { /* best-effort, matches LogsController.Clear */ }
            }
            return Ok();
        }
    }
}
