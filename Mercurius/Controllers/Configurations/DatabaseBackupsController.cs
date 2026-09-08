using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Mercurius.Models;
using Mercurius.Services;

namespace Mercurius.Controllers.Configurations
{
    [Authorize(Policy = Common.ModuleRegistry.Pages.CONFIG_DATABASE_BACKUPS)]
    public class DatabaseBackupsController : BaseController
    {
        private readonly DatabaseBackupService _backupService;
        private readonly bool _isSqliteProvider;

        public DatabaseBackupsController(IHttpContextAccessor httpContextAccessor, DatabaseBackupService backupService, IConfiguration configuration)
            : base(httpContextAccessor)
        {
            _backupService = backupService;
            // Backups only exist for the Sqlite provider (see Program.cs) — SQL Server hosting
            // providers manage their own backups.
            _isSqliteProvider = !string.Equals(configuration.GetValue<string>("DatabaseProvider"), "SqlServer", StringComparison.OrdinalIgnoreCase);
        }

        public IActionResult Index(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (!_isSqliteProvider) return NotFound();
            var backupDirectory = _backupService.BackupDirectory;
            var backups = new List<BackupFileInfo>();
            if (Directory.Exists(backupDirectory))
            {
                foreach (var file in Directory.GetFiles(backupDirectory, "*.sqlite").OrderByDescending(f => f))
                {
                    var fi = new FileInfo(file);
                    backups.Add(new BackupFileInfo { FileName = fi.Name, CreatedDate = fi.CreationTime, Size = fi.Length });
                }
            }
            return View(backups);
        }

        public IActionResult Download(string fileName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (!_isSqliteProvider) return NotFound();
            var backupDir = Path.GetFullPath(_backupService.BackupDirectory);
            var fp = Path.GetFullPath(Path.Combine(backupDir, fileName));
            if (!fp.StartsWith(backupDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !System.IO.File.Exists(fp)) return NotFound();
            return File(System.IO.File.ReadAllBytes(fp), "application/octet-stream", fileName);
        }
    }
}
