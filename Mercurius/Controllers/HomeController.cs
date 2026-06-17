using System;
using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Mercurius.Models;

namespace Mercurius.Controllers
{
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly IWebHostEnvironment _env;

        public HomeController(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment env)
            : base(httpContextAccessor)
        {
            _env = env;
        }

        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return View();
        }

        [AllowAnonymous]
        public IActionResult Error(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var model = new ErrorViewModel
            {
                RequestId = HttpContext.TraceIdentifier,
            };

            // In Development, surface the matching log block so the error page
            // actually shows what failed instead of just displaying the request id.
            if (_env.IsDevelopment())
            {
                model.LogExcerpt = ReadLogExcerpt(HttpContext.TraceIdentifier);
            }

            return View(model);
        }

        /// <summary>
        /// Reads today's log file and returns the UNHANDLED EXCEPTION block whose
        /// 'Request:' line mentions <paramref name="requestId"/>, plus a few
        /// surrounding lines for context.
        /// </summary>
        private string? ReadLogExcerpt(string? requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return null;
            try
            {
                var overrideDir = System.Environment.GetEnvironmentVariable("MERCURIUS_LOG_DIR");
                var logDir = !string.IsNullOrWhiteSpace(overrideDir)
                    ? overrideDir!
                    : Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                        "Mercurius", "logs");
                var logFile = Path.Combine(logDir, $"mercurius_{DateTime.UtcNow:yyyyMMdd}.log");
                if (!System.IO.File.Exists(logFile)) return null;

                // Tail the file - the matching block is always near the end.
                var lines = ReadTail(logFile, maxLines: 6000);
                var startIdx = -1;
                for (var i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Contains("UNHANDLED EXCEPTION", StringComparison.Ordinal))
                    {
                        // Look ahead for the Request line.
                        for (var j = i; j < Math.Min(i + 20, lines.Count); j++)
                        {
                            if (lines[j].Contains(requestId, StringComparison.Ordinal))
                            {
                                startIdx = i;
                                break;
                            }
                        }
                        if (startIdx != -1) break;
                    }
                }
                if (startIdx == -1) return null;

                // Trim to end-of-block: next UNHANDLED EXCEPTION or end of file.
                var endIdx = lines.Count;
                for (var k = startIdx + 1; k < lines.Count; k++)
                {
                    if (lines[k].Contains("UNHANDLED EXCEPTION", StringComparison.Ordinal))
                    {
                        endIdx = k;
                        break;
                    }
                }
                return string.Join('\n', lines.GetRange(startIdx, endIdx - startIdx));
            }
            catch
            {
                return null;
            }
        }

        private static System.Collections.Generic.List<string> ReadTail(string path, int maxLines)
        {
            var buffer = new System.Collections.Generic.List<string>(maxLines);
            using var fs = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                buffer.Add(line);
                if (buffer.Count > maxLines) buffer.RemoveAt(0);
            }
            return buffer;
        }
    }
}
